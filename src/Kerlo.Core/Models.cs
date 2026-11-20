namespace Kerlo.Core;

public sealed record ProcessSample(int Pid, string Name, long WorkingSetBytes, double CpuPercent);

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
