using Kerlo.Core;
using Kerlo.Services;

void Assert(bool check, string message)
{
    if (!check) throw new InvalidOperationException("Windows integration failure: " + message);
    Console.WriteLine("PASS: " + message);
}

Console.WriteLine("Testing real Windows metrics, no synthetic sensor input.");
using var monitor = new WindowsSampler();
var first = monitor.Sample();
await Task.Delay(1500);
var second = monitor.Sample();

Assert(first.TotalMemoryBytes > 0, "Physical memory counter returned a positive capacity");
Assert(second.UsedMemoryBytes > 0 && second.UsedMemoryBytes <= second.TotalMemoryBytes,
    "Memory used is within installed physical RAM");
Assert(second.CpuPercent >= 0 && second.CpuPercent <= 100, "CPU measurement within valid bounds");
Assert(second.Timestamp > first.Timestamp, "Windows samples advance in time");
Assert(second.Processes.Count >= 1, "Process enumeration returns live processes");
var systemSnapshot = System.Diagnostics.Process.GetProcesses();
var systemProcessCount = systemSnapshot.Length;
foreach (var process in systemSnapshot) process.Dispose();
// Process churn is expected between snapshots; losing half the list is not.
Assert(second.Processes.Count >= Math.Max(1, (int)Math.Floor(systemProcessCount * 0.80)),
    $"Process enumeration covers at least 80% of a contemporaneous Windows PID snapshot ({second.Processes.Count}/{systemProcessCount})");
Assert(second.Processes.Select(p => p.Pid).Distinct().Count() == second.Processes.Count,
    "Process enumeration contains unique PIDs");
Assert(second.Processes.All(p => p.WorkingSetBytes >= 0 && p.CpuPercent is >= 0 and <= 100),
    "Per-process metrics remain in valid bounds");
Assert(second.DownloadBytesPerSecond >= 0 && second.UploadBytesPerSecond >= 0,
    "Network sampling produces nonnegative rates");
Assert(second.DiskBytesPerSecond is null or >= 0,
    "Optional disk rate is unavailable or nonnegative");
Assert(second.Gpu3DPercent is null or >= 0 and <= 100,
    "Optional GPU 3D engine counter is unavailable or bounded");
var groups = ProcessGrouping.Group(second.Processes);
Assert(groups.Count >= 1, "Live process groups are generated");
var registry = StartupReader.Read();
Assert(registry.All(x => x.Name != null && x.Command != null), "Startup reader handled registry safely");

Console.WriteLine($"Windows telemetry checks passed: {second.Processes.Count} visible processes, " +
    $"CPU {Calculations.Percent(second.CpuPercent)}, RAM {Calculations.Percent(second.MemoryPercent)}, " +
    $"GPU 3D {(second.Gpu3DPercent.HasValue ? Calculations.Percent(second.Gpu3DPercent.Value) : "unavailable")}, " +
    $"startup entries {registry.Count}.");
