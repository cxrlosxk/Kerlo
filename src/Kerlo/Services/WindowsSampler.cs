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
        if (_lastGpuDiscover == default || (now - _lastGpuDiscover).TotalSeconds >= 30)
        {
            _lastGpuDiscover = now;
            foreach (var c in _gpuCounters) c.Dispose();
            _gpuCounters.Clear();
            try
            {
                if (PerformanceCounterCategory.Exists("GPU Engine"))
                {
                    var category = new PerformanceCounterCategory("GPU Engine");
                    var names = category.GetInstanceNames()
                        .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)).ToArray();
                    // Incomplete enumeration would yield misleading numbers.
                    if (names.Length > 0 && names.Length <= 128)
                    {
                        foreach (var name in names)
                        {
                            try
                            {
                                var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                                _ = counter.NextValue();
                                _gpuCounters.Add(counter);
                            }
                            catch { /* Instance may have exited. */ }
                        }
                    }
                }
            }
            catch { /* GPU driver or performance-counter provider may be unavailable. */ }
        }
        if (_gpuCounters.Count == 0) return null;
        double total = 0;
        var good = 0;
        foreach (var counter in _gpuCounters)
        {
            try { total += Math.Max(0, counter.NextValue()); good++; }
            catch { /* A GPU process ended between samples. */ }
        }
        return good == 0 ? null : Math.Clamp(total, 0, 100);
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
