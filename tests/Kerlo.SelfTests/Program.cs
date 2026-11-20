using Kerlo.Core;

var count = 0;
void Check(bool condition, string name)
{
    count++;
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

Check(Calculations.CpuPercentage(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 4) == 25, "CPU core normalization");
Check(Calculations.CpuPercentage(TimeSpan.Zero, TimeSpan.Zero, 4) == 0, "zero elapsed guard");
Check(Calculations.CpuPercentage(TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(1), 4) == 100, "CPU percent clamp");
Check(Calculations.Rate(5000, 1000, TimeSpan.FromSeconds(2)) == 2000, "byte rate");
Check(Calculations.Rate(1, 1000, TimeSpan.FromSeconds(2)) == 0, "reset counter guard");
Check(Calculations.Bytes(1024) == "1 KB", "binary unit formatting");
Check(Calculations.Bytes(1048576, true) == "1 MB/s", "throughput formatting");
Check(ProcessGrouping.Normalize("chrome.exe") == "Google Chrome", "browser normalization");
Check(ProcessGrouping.Normalize("steamwebhelper") == "Steam", "app helper grouping");
Check(ProcessGrouping.Normalize("unrecognized") == "unrecognized", "unknown process retained");

var list = new[] { new ProcessSample(1, "chrome", 1_000_000, 8),
    new ProcessSample(2, "chrome.exe", 2_000_000, 7),
    new ProcessSample(3, "dwm", 600_000, 1) };
var groups = ProcessGrouping.Group(list);
Check(groups.Count == 2 && groups[0].Name == "Google Chrome", "process grouping and ordering");
Check(groups[0].ProcessCount == 2 && groups[0].WorkingSetBytes == 3_000_000, "memory aggregation");
Check(groups[0].CpuPercent == 15 && groups[0].Processes.Count == 2, "CPU aggregation");
var now = DateTimeOffset.UtcNow;
MachineSample Sample(double cpu, ulong used, ulong total, double? gpu = 10) =>
    new(now, cpu, used, total, 10, 20, null, gpu, list);
Check(Math.Abs(Sample(10, 75, 100).MemoryPercent - 75) < 0.01, "memory percentage");
Check(Sample(10, 2, 0).MemoryPercent == 0, "zero memory guard");
Check(Insights.Evaluate(Sample(95, 90, 100, null), groups).Any(i => i.Title == "High CPU demand"), "high CPU insight");
Check(Insights.Evaluate(Sample(10, 90, 100), groups).Any(i => i.Title == "Memory pressure"), "memory pressure insight");
Check(Insights.Evaluate(Sample(10, 10, 100, null), groups).Any(i => i.Title == "GPU telemetry unavailable"), "unavailable GPU disclosure");
var history = new SampleHistory(2);
history.Add(Sample(10, 10, 100));
history.Add(Sample(20, 20, 100));
history.Add(Sample(30, 30, 100));
Check(history.Samples.Count == 2 && history.Samples[0].CpuPercent == 20, "bounded history eviction");
var csv = CsvExport.Write(history.Samples);
Check(csv.Contains("Timestamp,CpuPercent") && csv.Split("\r\n").Length == 4, "CSV export schema and row count");
Console.WriteLine($"All {count} core checks passed.");
