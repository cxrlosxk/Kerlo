using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Kerlo.Core;
using Kerlo.Services;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Kerlo;

public partial class MainWindow : Window
{
    private readonly WindowsSampler _sampler = new();
    private readonly SampleHistory _history = new(1800);
    private readonly DispatcherTimer _timer = new();
    private KerloSettings _settings = SettingsStore.Load();
    private MachineSample? _lastSample;
    private IReadOnlyList<AppGroup> _groups = [];
    private readonly ObservableCollection<GroupRow> _groupRows = new();
    private readonly ObservableCollection<ProcessRow> _detailRows = new();
    private Forms.NotifyIcon? _tray;
    private bool _sampling;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        // Bind each grid exactly once; replacing ItemsSource every 2 seconds
        // previously reset scrolling, sorting and process selection.
        GroupsGrid.ItemsSource = _groupRows;
        ProcessDetailsGrid.ItemsSource = _detailRows;
        _timer.Tick += async (_, _) => await RefreshAsync();
        Topmost = _settings.AlwaysOnTop;
        SelectNavigation("Overview");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _initialized = true;
        TopmostOption.IsChecked = _settings.AlwaysOnTop;
        var refreshIndex = _settings.SafeRefreshSeconds switch { 1 => 0, 5 => 2, _ => 1 };
        RefreshCombo.SelectedIndex = refreshIndex;
        _timer.Interval = TimeSpan.FromSeconds(_settings.SafeRefreshSeconds);
        CreateTray();
        StartupGrid.ItemsSource = StartupReader.Read();
        StatusText.Text = Environment.MachineName + "  ·  Live system readings";
        await RefreshAsync();
        _timer.Start();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
        _tray?.Dispose();
        _sampler.Dispose();
    }

    private async Task RefreshAsync()
    {
        if (_sampling) return;
        _sampling = true;
        try
        {
            var sample = await Task.Run(() => _sampler.Sample());
            _lastSample = sample;
            _history.Add(sample);
            _groups = ProcessGrouping.Group(sample.Processes);
            PopulateHeader(sample);
            PopulateDashboard(sample);
            PopulateHistory(sample);
            PopulateGroups();
            InsightList.ItemsSource = Insights.Evaluate(sample, _groups, _sampler.GpuWarmingUp);
            UpdatedText.Text = "Updated " + sample.Timestamp.ToLocalTime().ToString("h:mm:ss tt");
            StatusText.Text = $"{sample.Processes.Count} visible processes  ·  {sample.Timestamp.ToLocalTime():h:mm:ss tt}";
            if (_tray != null)
                _tray.Text = $"Kerlo · CPU {Calculations.Percent(sample.CpuPercent)} · RAM {Calculations.Percent(sample.MemoryPercent)}";
        }
        catch (Exception ex)
        {
            UpdatedText.Text = "Reading unavailable";
            StatusText.Text = "Could not sample system: " + ex.Message;
        }
        finally { _sampling = false; }
    }

    private void PopulateHeader(MachineSample s)
    {
        CpuMetric.Text = Calculations.Percent(s.CpuPercent);
        CpuMeter.Value = s.CpuPercent;
        MemoryMetric.Text = Calculations.Percent(s.MemoryPercent);
        MemoryMeter.Value = s.MemoryPercent;
        MemoryDetails.Text = $"{Calculations.Bytes(s.UsedMemoryBytes)} / {Calculations.Bytes(s.TotalMemoryBytes)}";
        GpuMetric.Text = s.Gpu3DPercent is { } gpu
            ? Calculations.Percent(gpu)
            : _sampler.GpuWarmingUp ? "…" : "N/A";
        GpuMeter.Value = s.Gpu3DPercent ?? 0;
        GpuMetric.ToolTip = _sampler.GpuCounterStatus;
        GpuStatusText.Text = _sampler.GpuWarmingUp
            ? "Initializing 3D counters…"
            : s.Gpu3DPercent.HasValue ? "Windows 3D engine counters"
            : "Unavailable · hover for details";
        GpuStatusText.ToolTip = _sampler.GpuCounterStatus;
        DiskMetric.Text = s.DiskBytesPerSecond is { } disk ? Calculations.Bytes(disk, true) : "N/A";
        DownloadMetric.Text = Calculations.Bytes(s.DownloadBytesPerSecond, true);
        UploadMetric.Text = Calculations.Bytes(s.UploadBytesPerSecond, true);
    }

    private void PopulateDashboard(MachineSample s)
    {
        OverviewCpuChart.Update(_history.Samples.Select(h => h.CpuPercent), Color.FromRgb(155, 218, 203));
        DashboardGroups.ItemsSource = _groups.Take(7).Select(GroupRow.From).ToArray();
    }

    private void PopulateHistory(MachineSample s)
    {
        var samples = _history.Samples;
        CpuHistoryChart.Update(samples.Select(h => h.CpuPercent), Color.FromRgb(155, 218, 203));
        MemoryHistoryChart.Update(samples.Select(h => h.MemoryPercent), Color.FromRgb(144, 184, 242));
        var maxNetwork = Math.Max(1024, samples.Max(h => h.DownloadBytesPerSecond) * 1.1);
        NetworkHistoryChart.Update(samples.Select(h => h.DownloadBytesPerSecond), Color.FromRgb(218, 179, 128), maxNetwork);
        HistoryCpuValue.Text = Calculations.Percent(s.CpuPercent);
        HistoryMemoryValue.Text = Calculations.Percent(s.MemoryPercent);
        HistoryNetworkValue.Text = Calculations.Bytes(s.DownloadBytesPerSecond, true);
    }

    private void PopulateGroups()
    {
        if (_lastSample is null) return;
        var query = SearchBox.Text?.Trim() ?? "";
        var filtered = _groups.Where(g => query.Length == 0 ||
            g.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            g.Processes.Any(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();

        // Keep GroupRow object identities stable so the DataGrid can maintain
        // scroll position, user-selected sorts and keyboard/row selection.
        var incoming = filtered.Select(GroupRow.From).ToArray();
        ReconcileRows(_groupRows, incoming,
            row => row.Name, (oldRow, newRow) => oldRow.Update(newRow.Model));
        if (GroupsGrid.SelectedItem is not GroupRow && _groupRows.Count > 0 &&
            ProcessesPage.Visibility == Visibility.Visible)
            GroupsGrid.SelectedItem = _groupRows[0];
        RefreshProcessDetails();
    }

    private void GroupsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshProcessDetails();

    private void RefreshProcessDetails()
    {
        if (GroupsGrid.SelectedItem is not GroupRow row)
        {
            GroupNameText.Text = "Select an application";
            GroupDescriptionText.Text = "Individual processes will appear here.";
            _detailRows.Clear();
            return;
        }
        GroupNameText.Text = row.Name + $" · {row.ProcessCount} process(es)";
        GroupDescriptionText.Text = row.Model.Description;
        var incoming = row.Model.Processes.Select(ProcessRow.From).ToArray();
        ReconcileRows(_detailRows, incoming,
            process => process.Pid, (oldRow, newRow) => oldRow.Update(newRow.Model));
    }

    private static void ReconcileRows<T, TKey>(
        ObservableCollection<T> existing, IReadOnlyList<T> incoming,
        Func<T, TKey> key, Action<T, T> update) where TKey : notnull
    {
        // In-place updates avoid the expensive visual reset caused by assigning
        // ItemsSource on every timer tick. Typical group counts are modest.
        for (int i = 0; i < incoming.Count; i++)
        {
            var wanted = key(incoming[i]);
            if (i < existing.Count && EqualityComparer<TKey>.Default.Equals(key(existing[i]), wanted))
            {
                update(existing[i], incoming[i]);
                continue;
            }
            int found = -1;
            for (int j = i + 1; j < existing.Count; j++)
            {
                if (EqualityComparer<TKey>.Default.Equals(key(existing[j]), wanted))
                {
                    found = j;
                    break;
                }
            }
            if (found >= 0)
            {
                existing.Move(found, i);
                update(existing[i], incoming[i]);
            }
            else
                existing.Insert(i, incoming[i]);
        }
        while (existing.Count > incoming.Count)
            existing.RemoveAt(existing.Count - 1);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initialized) PopulateGroups();
    }

    private void Navigate(string destination)
    {
        DashboardPage.Visibility = destination == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        ProcessesPage.Visibility = destination == "Processes" ? Visibility.Visible : Visibility.Collapsed;
        PerformancePage.Visibility = destination == "Performance" ? Visibility.Visible : Visibility.Collapsed;
        StartupPage.Visibility = destination == "Startup" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = destination == "Preferences" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = destination switch
        {
            "Processes" => "Every process, in context.",
            "Performance" => "Your system, over time.",
            "Startup" => "A closer look at startup.",
            "Preferences" => "Make Kerlo yours.",
            _ => "A clearer picture of your PC."
        };
        PageSubtitle.Text = destination switch
        {
            "Processes" => "Inspect grouped apps, understand their footprint, and take careful action.",
            "Performance" => "Live trends from this session, with a local CSV export.",
            "Startup" => "Review standard Windows startup registry entries.",
            "Preferences" => "Stay in control of sampling, privacy, and window behavior.",
            _ => "Live measurements from your Windows system."
        };
        PageSectionText.Text = destination switch
        {
            "Processes" => "PROCESS INSPECTOR",
            "Performance" => "PERFORMANCE HISTORY",
            "Startup" => "WINDOWS STARTUP",
            "Preferences" => "PREFERENCES",
            _ => "SYSTEM OVERVIEW"
        };
        SelectNavigation(destination);
        // The same ScrollViewer hosts every page; switching pages must reset the old offset.
        ContentScrollViewer.Dispatcher.BeginInvoke(
            () => ContentScrollViewer.ScrollToTop(), DispatcherPriority.Loaded);
        if (destination == "Processes") PopulateGroups();
    }

    private void SelectNavigation(string destination)
    {
        foreach (var item in new (Button Button, string Name)[]
        {
            (DashboardNav, "Overview"), (ProcessesNav, "Processes"),
            (PerformanceNav, "Performance"), (StartupNav, "Startup"), (SettingsNav, "Preferences")
        })
        {
            item.Button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                item.Name == destination ? "#25433D" : "#111923"));
            item.Button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                item.Name == destination ? "#ADE5D8" : "#ADBDC4"));
        }
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) => Navigate("Overview");
    private void Processes_Click(object sender, RoutedEventArgs e) => Navigate("Processes");
    private void Performance_Click(object sender, RoutedEventArgs e) => Navigate("Performance");
    private void Startup_Click(object sender, RoutedEventArgs e) => Navigate("Startup");
    private void Settings_Click(object sender, RoutedEventArgs e) => Navigate("Preferences");

    private void RefreshStartup_Click(object sender, RoutedEventArgs e) => StartupGrid.ItemsSource = StartupReader.Read();

    private void RefreshCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || RefreshCombo.SelectedItem is not ComboBoxItem item ||
            !int.TryParse(item.Tag?.ToString(), out var seconds)) return;
        _settings = _settings with { RefreshSeconds = seconds };
        _timer.Interval = TimeSpan.FromSeconds(seconds);
        SettingsStore.Save(_settings);
    }

    private void TopmostOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        Topmost = TopmostOption.IsChecked == true;
        _settings = _settings with { AlwaysOnTop = Topmost };
        SettingsStore.Save(_settings);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Samples.Count == 0)
        {
            MessageBox.Show(this, "No measurements have been collected yet.", "Kerlo",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = "kerlo-session-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv",
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, CsvExport.Write(_history.Samples));
            StatusText.Text = "Exported session history to " + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not export CSV: " + ex.Message, "Kerlo",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EndProcess_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessDetailsGrid.SelectedItem is not ProcessRow row)
        {
            MessageBox.Show(this, "Select an individual process in the lower table first.", "Kerlo");
            return;
        }

        string[] protectedNames =
            ["system", "idle", "csrss", "smss", "wininit", "services", "lsass", "svchost"];
        if (row.Pid <= 4 || protectedNames.Contains(row.Name, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Kerlo blocks termination of core Windows processes in this preview.",
                "Protected process", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var confirm = MessageBox.Show(this,
            $"End {row.Name} (PID {row.Pid})?\n\nUnsaved work in this process may be lost. Kerlo will not terminate related processes.",
            "Confirm process termination", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;
        try
        {
            using var process = Process.GetProcessById(row.Pid);
            if (!process.HasExited && !process.ProcessName.Equals(row.Name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The PID now belongs to a different process. Refresh before trying again.");
            if (!process.HasExited) process.Kill(entireProcessTree: false);
            StatusText.Text = "Termination requested for PID " + row.Pid;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not end that process: " + ex.Message, "Kerlo",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessDetailsGrid.SelectedItem is not ProcessRow row)
        {
            MessageBox.Show(this, "Select an individual process in the lower table first.", "Kerlo");
            return;
        }
        try
        {
            using var process = Process.GetProcessById(row.Pid);
            var path = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("The executable path is unavailable.");
            Process.Start(new ProcessStartInfo("explorer.exe",
                $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Cannot open this process location: " + ex.Message, "Kerlo",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CreateTray()
    {
        try
        {
            var context = new Forms.ContextMenuStrip();
            context.Items.Add("Show Kerlo", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
            context.Items.Add("Quit Kerlo", null, (_, _) => Dispatcher.Invoke(Close));
            _tray = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "Kerlo · Activity Monitor",
                Visible = true,
                ContextMenuStrip = context
            };
            _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        }
        catch { /* The main monitoring window works without a tray icon. */ }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public sealed class GroupRow : INotifyPropertyChanged
    {
        public AppGroup Model { get; private set; }
        private GroupRow(AppGroup model) => Model = model;
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Name => Model.Name;
        public int ProcessCount => Model.ProcessCount;
        public string CpuDisplay =>
            Model.Processes.All(p => !p.CpuAvailable) ? "N/A" :
            (Model.Processes.Any(p => !p.CpuAvailable) ? "≥ " : "") +
            Calculations.Percent(Model.CpuPercent);
        public string MemoryDisplay =>
            Model.Processes.All(p => !p.MemoryAvailable) ? "N/A" :
            (Model.Processes.Any(p => !p.MemoryAvailable) ? "≥ " : "") +
            Calculations.Bytes(Model.WorkingSetBytes);
        public static GroupRow From(AppGroup g) => new(g);

        public void Update(AppGroup updated)
        {
            if (Name != updated.Name)
                throw new InvalidOperationException("A group row may only be updated for the same application.");
            var previousCount = ProcessCount;
            var previousCpu = CpuDisplay;
            var previousMemory = MemoryDisplay;
            Model = updated;
            if (previousCount != ProcessCount)
                PropertyChanged?.Invoke(this, new(nameof(ProcessCount)));
            if (previousCpu != CpuDisplay)
                PropertyChanged?.Invoke(this, new(nameof(CpuDisplay)));
            if (previousMemory != MemoryDisplay)
                PropertyChanged?.Invoke(this, new(nameof(MemoryDisplay)));
        }
    }

    public sealed class ProcessRow : INotifyPropertyChanged
    {
        public ProcessSample Model { get; private set; }
        private ProcessRow(ProcessSample model) => Model = model;
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Pid => Model.Pid;
        public string Name => Model.Name;
        public string CpuDisplay => Model.CpuAvailable ? Calculations.Percent(Model.CpuPercent) : "N/A";
        public string MemoryDisplay => Model.MemoryAvailable ? Calculations.Bytes(Model.WorkingSetBytes) : "N/A";
        public static ProcessRow From(ProcessSample p) => new(p);

        public void Update(ProcessSample updated)
        {
            if (Pid != updated.Pid)
                throw new InvalidOperationException("A process row may only be updated for the same PID.");
            var previousName = Name;
            var previousCpu = CpuDisplay;
            var previousMemory = MemoryDisplay;
            Model = updated;
            if (previousName != Name)
                PropertyChanged?.Invoke(this, new(nameof(Name)));
            if (previousCpu != CpuDisplay)
                PropertyChanged?.Invoke(this, new(nameof(CpuDisplay)));
            if (previousMemory != MemoryDisplay)
                PropertyChanged?.Invoke(this, new(nameof(MemoryDisplay)));
        }
    }
}
