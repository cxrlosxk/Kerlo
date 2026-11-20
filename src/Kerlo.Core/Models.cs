namespace Kerlo.Core;

/// <summary>
  /// CPU and working-set bytes are independent telemetry readings. Protected processes
  /// are still enumerated even when one or both measurements are inaccessible.
  /// </summary>
public sealed record ProcessSample(
    int Pid, string Name, long WorkingSetBytes, double CpuPercent,
    bool CpuAvailable = true, bool MemoryAvailable = true);

public sealed record AppGroup(
    string Name,
    int ProcessCount,
    long WorkingSetBytes,
    double CpuPercent,
    string Description,
    IReadOnlyList<ProcessSample> Processes);

public sealed record MachineSample(
    DateTimeOffset Timestamp,
    double CpuPercent,
    ulong UsedMemoryBytes,
    ulong TotalMemoryBytes,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    double? DiskBytesPerSecond,
    double? Gpu3DPercent,
    IReadOnlyList<ProcessSample> Processes)
{
    public double MemoryPercent => TotalMemoryBytes == 0
        ? 0
        : Math.Clamp(100.0 * UsedMemoryBytes / TotalMemoryBytes, 0, 100);
}

public sealed record Insight(string Title, string Explanation, InsightSeverity Severity);
public enum InsightSeverity { Information, Attention }

public sealed record StartupItem(string Name, string Command, string RegistryLocation);
