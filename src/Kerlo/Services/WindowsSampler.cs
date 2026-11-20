using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Kerlo.Core;

namespace Kerlo.Services;

/// <summary>
/// Reads real Windows counters. Every optional counter returns null on failure; no simulated telemetry.
/// Sample() is serialized by the caller on a background thread.
/// </summary>
public sealed class WindowsSampler : IDisposable
{
    private readonly Dictionary<int, TimeSpan> _previousProcessCpu = [];
    private readonly Dictionary<string, (long Received, long Sent)> _previousNetwork = [];
    private readonly List<PerformanceCounter> _gpuCounters = [];
    private PerformanceCounter? _diskCounter;
    private DateTimeOffset _lastSampleAt;
    private DateTimeOffset _lastGpuDiscover;
    private bool _hasSystemTimes;
    private ulong _idleLast, _kernelLast, _userLast;
    private bool _disposed;
    private string _gpuCounterStatus = "Checking Windows GPU Engine performance counters...";
    public string GpuCounterStatus => _gpuCounterStatus;
    public bool GpuWarmingUp { get; private set; }

    // The Windows integration harness can exercise the 30-second GPU refresh path
    // immediately, without making every CI run wait 30 seconds.
    internal void RequestGpuRediscoveryForTesting() => _lastGpuDiscover = default;

    public WindowsSampler()
    {
        try
        {
            _diskCounter = new PerformanceCounter("PhysicalDisk", "Disk Bytes/sec", "_Total", true);
            _ = _diskCounter.NextValue();
        }
        catch { _diskCounter?.Dispose(); _diskCounter = null; }
    }

    public MachineSample Sample()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsSampler));
        var now = DateTimeOffset.UtcNow;
        var elapsed = _lastSampleAt == default ? TimeSpan.Zero : now - _lastSampleAt;
        var cpu = ReadSystemCpu();
        var (used, total) = ReadMemory();
        var processes = ReadProcesses(elapsed);
        var (download, upload) = ReadNetwork(elapsed);
        var disk = ReadDisk();
        var gpu = ReadGpu3D(now);
        _lastSampleAt = now;
        return new MachineSample(now, cpu, used, total, download, upload, disk, gpu, processes);
    }

    private double ReadSystemCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var currentIdle = idle.Value;
        var currentKernel = kernel.Value;
        var currentUser = user.Value;
        double result = 0;
        if (_hasSystemTimes && currentIdle >= _idleLast && currentKernel >= _kernelLast && currentUser >= _userLast)
        {
            var idleDelta = currentIdle - _idleLast;
            var totalDelta = (currentKernel - _kernelLast) + (currentUser - _userLast);
            if (totalDelta > 0)
                result = Math.Clamp(100.0 * (1.0 - (double)idleDelta / totalDelta), 0, 100);
        }
        (_idleLast, _kernelLast, _userLast) = (currentIdle, currentKernel, currentUser);
        _hasSystemTimes = true;
        return result;
    }

    private static (ulong Used, ulong Total) ReadMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys < status.AvailPhys) return (0, 0);
        return (status.TotalPhys - status.AvailPhys, status.TotalPhys);
    }

    private IReadOnlyList<ProcessSample> ReadProcesses(TimeSpan elapsed)
    {
        var readings = new List<ProcessSample>();
        var next = new Dictionary<int, TimeSpan>();

        // Windows exposes more process IDs than unprivileged callers can inspect.
        // Do not omit a live PID merely because CPU or working-set access is denied.
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                int pid;
                try { pid = process.Id; }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                { continue; }

                string name;
                try { name = process.ProcessName; }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                { name = "Protected process"; }

                long workingSet = 0;
                bool memoryAvailable = false;
                try
                {
                    workingSet = Math.Max(0, process.WorkingSet64);
                    memoryAvailable = true;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                { /* Keep the PID but do not invent a working-set value. */ }

                double cpu = 0;
                bool cpuAvailable = false;
                try
                {
                    var totalTime = process.TotalProcessorTime;
                    cpu = _previousProcessCpu.TryGetValue(pid, out var old) && totalTime >= old
                        ? Calculations.CpuPercentage(totalTime - old, elapsed, Environment.ProcessorCount)
                        : 0;
                    cpuAvailable = true;
                    next[pid] = totalTime;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                { /* Inaccessible CPU time is unavailable, not zero. */ }

                readings.Add(new ProcessSample(pid, name, workingSet, cpu,
                    cpuAvailable, memoryAvailable));
            }
        }
        _previousProcessCpu.Clear();
        foreach (var entry in next) _previousProcessCpu[entry.Key] = entry.Value;
        return readings;
    }

    private (double Download, double Upload) ReadNetwork(TimeSpan elapsed)
    {
        double down = 0, up = 0;
        var next = new Dictionary<string, (long Received, long Sent)>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;
                try
                {
                    var stats = nic.GetIPv4Statistics();
                    var key = nic.Id;
                    next[key] = (stats.BytesReceived, stats.BytesSent);
                    if (_previousNetwork.TryGetValue(key, out var old))
                    {
                        down += Calculations.Rate(stats.BytesReceived, old.Received, elapsed);
                        up += Calculations.Rate(stats.BytesSent, old.Sent, elapsed);
                    }
                }
                catch (NetworkInformationException) { }
            }
        }
        catch (NetworkInformationException) { }
        _previousNetwork.Clear();
        foreach (var entry in next) _previousNetwork[entry.Key] = entry.Value;
        return (down, up);
    }

    private double? ReadDisk()
    {
        if (_diskCounter is null) return null;
        try { return Math.Max(0, _diskCounter.NextValue()); }
        catch { _diskCounter.Dispose(); _diskCounter = null; return null; }
    }

    private double? ReadGpu3D(DateTimeOffset now)
    {
        var refresh = _lastGpuDiscover == default ||
            (now - _lastGpuDiscover).TotalSeconds >= 30;
        List<PerformanceCounter>? nextCounters = null;

        if (refresh)
        {
            _lastGpuDiscover = now;
            var candidates = new List<PerformanceCounter>();
            try
            {
                if (!PerformanceCounterCategory.Exists("GPU Engine"))
                {
                    _gpuCounterStatus =
                        "Windows does not expose a GPU Engine counter category. Check the display driver and Windows performance-counter configuration.";
                }
                else
                {
                    var category = new PerformanceCounterCategory("GPU Engine");
                    var instances = category.GetInstanceNames();
                    var names = instances.Where(n =>
                        n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)).ToArray();
                    const int safetyCap = 256;

                    if (names.Length == 0)
                    {
                        _gpuCounterStatus = $"Windows exposed {instances.Length} GPU engine instance(s), but no active 3D engine instances. Task Manager's overall GPU percentage can include other engine types.";
                    }
                    else if (names.Length > safetyCap)
                    {
                        _gpuCounterStatus = $"Found {names.Length} GPU 3D instances, above Kerlo's {safetyCap}-counter sampling limit. Reporting a partial figure would be misleading.";
                    }
                    else
                    {
                        foreach (var name in names)
                        {
                            try
                            {
                                var counter = new PerformanceCounter(
                                    "GPU Engine", "Utilization Percentage", name, true);
                                _ = counter.NextValue(); // Warm up before the next tick.
                                candidates.Add(counter);
                            }
                            catch (Exception ex) when (ex is not OutOfMemoryException)
                            {
                                // Instances can disappear between enumeration and opening.
                            }
                        }

                        if (candidates.Count == names.Length)
                        {
                            nextCounters = candidates;
                            _gpuCounterStatus =
                                $"Reading {names.Length} Windows GPU 3D engine instance(s). This is not Task Manager's total GPU utilization.";
                        }
                        else
                        {
                            _gpuCounterStatus =
                                $"Found {names.Length} GPU 3D instance(s), but opened {candidates.Count}. The partial sample has been discarded.";
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _gpuCounterStatus =
                    "GPU Engine enumeration failed (" + ex.GetType().Name +
                    "). Verify graphics-driver and performance-counter availability.";
            }
            finally
            {
                // Candidates become owned by _gpuCounters only on a complete read.
                if (nextCounters is null)
                    foreach (var counter in candidates) counter.Dispose();
            }
        }

        // Read the OLD, already-primed counters before swapping to the newly
        // discovered set. This avoids a fabricated N/A every 30 seconds.
        double? previousReading = null;
        if (_gpuCounters.Count > 0)
        {
            try
            {
                double total = 0;
                foreach (var counter in _gpuCounters)
                    total += Math.Max(0, counter.NextValue());
                previousReading = Math.Clamp(total, 0, 100);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _gpuCounterStatus =
                    "A previously active GPU 3D instance disappeared while sampling (" +
                    ex.GetType().Name + "). Counters will be rediscovered.";
                if (!refresh)
                    _lastGpuDiscover = default;
            }
        }

        if (refresh)
        {
            // Replace only after reading the old snapshot. Never sample a freshly
            // primed counter as if it had a full sampling interval.
            foreach (var counter in _gpuCounters) counter.Dispose();
            _gpuCounters.Clear();
            if (nextCounters is not null)
                _gpuCounters.AddRange(nextCounters);

            GpuWarmingUp = nextCounters is not null && previousReading is null;
            if (GpuWarmingUp)
                _gpuCounterStatus = "Initializing Windows GPU 3D engine counters. A measurement will be available after the next sampling interval.";

            // If discovery failed, old counters may omit newly created GPU work.
            // Do not publish an incomplete previous set as a current measurement.
            return nextCounters is null ? null : previousReading;
        }

        GpuWarmingUp = false;
        return previousReading;
    }

    public void Dispose()
    {
        _disposed = true;
        _diskCounter?.Dispose();
        foreach (var c in _gpuCounters) c.Dispose();
        _gpuCounters.Clear();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FileTime
    {
        public readonly uint Low;
        public readonly uint High;
        public ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
}
