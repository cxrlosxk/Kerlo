using System.Globalization;
using System.Text;

namespace Kerlo.Core;

public static class Calculations
{
    public static double CpuPercentage(TimeSpan processDelta, TimeSpan elapsed, int logicalProcessors)
    {
        if (elapsed <= TimeSpan.Zero || logicalProcessors <= 0) return 0;
        return Math.Clamp(processDelta.TotalMilliseconds / (elapsed.TotalMilliseconds * logicalProcessors) * 100.0, 0, 100);
    }

    public static double Rate(long current, long previous, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero || current < previous) return 0;
        return (current - previous) / elapsed.TotalSeconds;
    }

    public static string Bytes(double value, bool perSecond = false)
    {
        if (!double.IsFinite(value) || value < 0) value = 0;
        string[] units = ["B", "KB", "GB", "TB", "PB"];
        // Correct the unit order without relying on an external formatting library.
        units[2] = "MB"; units[3] = "GB"; units[4] = "TB";
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return value.ToString(value >= 100 ? "0" : "0.#", CultureInfo.InvariantCulture)
            + " " + units[unit] + (perSecond ? "/s" : "");
    }

    public static string Percent(double value) =>
        Math.Clamp(double.IsFinite(value) ? value : 0, 0, 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}

public static class ProcessGrouping
{
    private static readonly Dictionary<string, (string Group, string Info)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = ("Google Chrome", "Web browser. Multiple processes may represent tabs, extensions, and GPU work."),
        ["msedge"] = ("Microsoft Edge", "Web browser. Multiple processes are normal for tabs and extensions."),
        ["firefox"] = ("Mozilla Firefox", "Web browser with separate content and browser processes."),
        ["discord"] = ("Discord", "Chat application; helper processes and renderers are normal."),
        ["code"] = ("Visual Studio Code", "Editor with separate extensions, terminals, and language servers."),
        ["devenv"] = ("Visual Studio", "Development environment and its supporting processes."),
        ["claude"] = ("Claude", "AI development tool. Check individual sessions before ending a process."),
        ["node"] = ("Node.js", "JavaScript runtime used by applications and development servers."),
        ["python"] = ("Python", "Python interpreter or application; identify its command before ending."),
        ["powershell"] = ("PowerShell", "Windows scripting and automation host."),
        ["pwsh"] = ("PowerShell", "Windows scripting and automation host."),
        ["steam"] = ("Steam", "Game launcher and supporting background services."),
        ["steamwebhelper"] = ("Steam", "Steam web-rendering helper processes."),
        ["dwm"] = ("Desktop Window Manager", "Windows compositor. Ending it can interrupt the desktop."),
        ["explorer"] = ("Windows Explorer", "Windows shell, taskbar, and file explorer."),
        ["svchost"] = ("Windows Service Host", "Hosts Windows services; separate instances are expected."),
        ["searchhost"] = ("Windows Search", "Windows search experience and indexing support."),
        ["msmpeng"] = ("Microsoft Defender", "Built-in antivirus engine and background protection."),
        ["obs64"] = ("OBS Studio", "Recording and streaming application."),
        ["spotify"] = ("Spotify", "Music playback application."),
        ["applemusic"] = ("Apple Music", "Music playback application.")
    };

    public static string Normalize(string processName)
    {
        var raw = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4] : processName;
        return Known.TryGetValue(raw, out var value) ? value.Group : raw;
    }

    public static string Describe(string processName)
    {
        var raw = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4] : processName;
        return Known.TryGetValue(raw, out var value)
            ? value.Info : "Application or system process. Inspect its executable path before taking action.";
    }

    public static IReadOnlyList<AppGroup> Group(IEnumerable<ProcessSample> processes) =>
        processes.GroupBy(p => Normalize(p.Name), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var items = g.OrderByDescending(p => p.CpuPercent).ThenByDescending(p => p.WorkingSetBytes).ToArray();
                return new AppGroup(g.Key, items.Length,
                    items.Sum(p => Math.Max(0, p.WorkingSetBytes)),
                    Math.Clamp(items.Sum(p => p.CpuPercent), 0, 100),
                    Describe(items[0].Name), items);
            })
            .OrderByDescending(g => g.WorkingSetBytes)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public static class Insights
{
    public static IReadOnlyList<Insight> Evaluate(MachineSample sample, IReadOnlyList<AppGroup> groups)
    {
        List<Insight> insights = [];
        if (sample.MemoryPercent >= 85)
            insights.Add(new("Memory pressure", "Physical memory is above 85%. Inspect the largest application groups before closing anything.", InsightSeverity.Attention));
        if (sample.CpuPercent >= 90)
            insights.Add(new("High CPU demand", "The processor is heavily occupied. Sort processes by CPU usage to identify active workloads.", InsightSeverity.Attention));
        var busiest = groups.OrderByDescending(g => g.CpuPercent).FirstOrDefault();
        if (busiest is { CpuPercent: >= 25 })
            insights.Add(new("Notable CPU activity", $"{busiest.Name} currently accounts for about {Calculations.Percent(busiest.CpuPercent)} of total CPU capacity.", InsightSeverity.Information));
        if (sample.Gpu3DPercent is null)
            insights.Add(new("GPU telemetry unavailable", "The GPU 3D performance counter is not available on this machine. Kerlo will not invent a reading.", InsightSeverity.Information));
        if (insights.Count == 0)
            insights.Add(new("System looks steady", "No high CPU or memory pressure was observed in this sample. This is not a full hardware health assessment.", InsightSeverity.Information));
        return insights;
    }
}

public sealed class SampleHistory(int capacity)
{
    private readonly Queue<MachineSample> _samples = new();
    public int Capacity { get; } = Math.Max(1, capacity);
    public IReadOnlyList<MachineSample> Samples => _samples.ToArray();
    public void Add(MachineSample sample)
    {
        _samples.Enqueue(sample);
        while (_samples.Count > Capacity) _samples.Dequeue();
    }
}

public static class CsvExport
{
    public static string Write(IEnumerable<MachineSample> samples)
    {
        var sb = new StringBuilder("Timestamp,CpuPercent,MemoryPercent,MemoryUsedBytes,MemoryTotalBytes,Gpu3DPercent,DiskBytesPerSec,DownloadBytesPerSec,UploadBytesPerSec\r\n");
        foreach (var s in samples)
        {
            string Num(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);
            sb.Append(s.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(Num(s.CpuPercent)).Append(',').Append(Num(s.MemoryPercent)).Append(',')
                .Append(s.UsedMemoryBytes).Append(',').Append(s.TotalMemoryBytes).Append(',')
                .Append(s.Gpu3DPercent.HasValue ? Num(s.Gpu3DPercent.Value) : "").Append(',')
                .Append(s.DiskBytesPerSecond.HasValue ? Num(s.DiskBytesPerSecond.Value) : "").Append(',')
                .Append(Num(s.DownloadBytesPerSecond)).Append(',')
                .Append(Num(s.UploadBytesPerSecond)).Append("\r\n");
        }
        return sb.ToString();
    }
}
