using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FinanceOS.Core;
using FinanceOS.Infrastructure;

namespace FinanceOS.Desktop;

public partial class MainWindow
{
    private readonly SqliteProductivityStore _productivity;
    private readonly DispatcherTimer _automationTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool _automationBusy;
    private bool _productivityLoaded;
    private void InitializeProductivity()
    {
        _automationTimer.Tick += async (_, _) =>
        {
            if (!_ready || _automationBusy) return;
            _automationBusy = true;
            try { await _productivity.RunRulesAsync(DateOnly.FromDateTime(DateTime.Today)); }
            catch (Exception) { ProductivityStatusText.Text = "Automation could not run; it will retry."; }
            finally { _automationBusy = false; }
        };
        _automationTimer.Start(); Closed += (_, _) => _automationTimer.Stop();
    }
    private async Task<IReadOnlyList<WorkTask>> AllTasksAsync()
    {
        var active = await _tasks.ListAsync(new TaskQuery("Tasks", GlobalSearchBox.Text));
        var archived = await _tasks.ListAsync(new TaskQuery("Archive", GlobalSearchBox.Text));
        return active.Concat(archived).ToList();
    }
    private async Task RefreshProductivityAsync()
    {
        try
        {
            var settings = await _productivity.SettingsAsync();
            if (!_productivityLoaded)
            {
                ShowMetricsBox.IsChecked = settings.GetValueOrDefault("metrics", "true") == "true";
                ShowPlannerBox.IsChecked = settings.GetValueOrDefault("planner", "true") == "true";
                _productivityLoaded = true;
            }
            SavedFilterBox.ItemsSource = settings.Keys.Where(k => k.StartsWith("filter:", StringComparison.Ordinal)).Select(k => k[7..]).ToList();
            var tasks = await AllTasksAsync(); var today = DateOnly.FromDateTime(DateTime.Today);
            ProductivityTasksGrid.ItemsSource = tasks;
            var open = tasks.Count(t => t.Status == "Open"); var overdue = tasks.Count(t => t.Status == "Open" && t.DueDate < today);
            MetricsText.Text = $"Filtered workload: {open} open · {tasks.Count(t => t.Status == "Done")} done · {overdue} overdue ({(open == 0 ? 0 : 100 * overdue / open)}% of open)";
            MetricsText.Visibility = ShowMetricsBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            CompletionChart.Visibility = MetricsText.Visibility;
            CompletionChart.Children.Clear();
            CompletionChart.Children.Add(new TextBlock { Text = "Completions per day (UTC, recorded since this upgrade)" });
            var trend = await _productivity.CompletionTrendAsync();
            for (var offset = 6; offset >= 0; offset--)
            {
                var day = DateTime.UtcNow.Date.AddDays(-offset).ToString("yyyy-MM-dd");
                var count = trend.GetValueOrDefault(day);
                CompletionChart.Children.Add(new TextBlock { Text = $"{day}   {count}" });
                CompletionChart.Children.Add(new ProgressBar { Value = count, Maximum = Math.Max(1, trend.Values.DefaultIfEmpty(0).Max()), Height = 5, Margin = new Thickness(0, 2, 0, 5) });
            }
            PlannerPanel.Visibility = ShowPlannerBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (!int.TryParse(CapacityBox.Text, out var capacity) || capacity < 0 || capacity > 1440 || !int.TryParse(EstimateBox.Text, out var estimate) || estimate < 1 || estimate > 1440) throw new ArgumentException("Enter capacity 0–1440 and estimate 1–1440 minutes.");
            PlannerGrid.ItemsSource = WorkloadPlanner.Suggest(tasks, capacity, estimate);
            var entries = await _productivity.TimeEntriesAsync();
            var running = entries.FirstOrDefault(e => e.End is null);
            TimerText.Text = $"Recorded time: {entries.Where(e => e.End is not null).Sum(e => (e.End!.Value - e.Start).TotalMinutes):F1} minutes. " +
                (running is null ? "No active timer." : $"Timer active since {running.Start.LocalDateTime:g}. Stop it when finished; elapsed time includes time while the app is closed.");
            RulesBox.ItemsSource = await _productivity.RulesAsync();
            RuleHistoryGrid.ItemsSource = await _productivity.HistoryAsync();
        }
        catch (Exception ex) { ProductivityStatusText.Text = ex is ArgumentException ? ex.Message : "Productivity data could not be loaded."; }
    }
    private async Task ProductivityAction(Func<Task> action)
    {
        try { await action(); await RefreshProductivityAsync(); ProductivityStatusText.Text = "Saved locally."; }
        catch (Exception ex) { ProductivityStatusText.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "Action failed. Existing data was preserved."; }
    }
    private async void OnRefreshProductivity(object sender, RoutedEventArgs e) => await RefreshProductivityAsync();
    private async void OnSaveDashboard(object sender, RoutedEventArgs e) => await ProductivityAction(async () =>
    {
        await _productivity.SaveSettingAsync("metrics", ShowMetricsBox.IsChecked == true ? "true" : "false");
        await _productivity.SaveSettingAsync("planner", ShowPlannerBox.IsChecked == true ? "true" : "false");
    });
    private async void OnSaveFilter(object sender, RoutedEventArgs e) => await ProductivityAction(async () =>
    {
        if (string.IsNullOrWhiteSpace(GlobalSearchBox.Text)) throw new ArgumentException("Enter a search to save.");
        await _productivity.SaveSettingAsync("filter:" + GlobalSearchBox.Text.Trim(), GlobalSearchBox.Text.Trim());
    });
    private async void OnSavedFilter(object sender, SelectionChangedEventArgs e)
    {
        if (SavedFilterBox.SelectedItem is string filter) { GlobalSearchBox.Text = filter; await RefreshProductivityAsync(); }
    }
    private WorkTask SelectedProductivityTask() => ProductivityTasksGrid.SelectedItem as WorkTask ?? throw new ArgumentException("Select a task first.");
    private async void OnBulkUrgent(object sender, RoutedEventArgs e) => await ProductivityAction(async () =>
    {
        var ids = ProductivityTasksGrid.SelectedItems.Cast<WorkTask>().Select(t => t.Id).ToList();
        if (ids.Count == 0) throw new ArgumentException("Select at least one task.");
        await _productivity.SetPrioritiesAsync(ids, "Urgent");
    });
    private async void OnStartTimer(object sender, RoutedEventArgs e) => await ProductivityAction(() => _productivity.StartTimerAsync(SelectedProductivityTask().Id, DateTimeOffset.UtcNow));
    private async void OnStopTimer(object sender, RoutedEventArgs e) => await ProductivityAction(() => _productivity.StopTimerAsync(DateTimeOffset.UtcNow));
    private async void OnSaveTaskTemplate(object sender, RoutedEventArgs e) => await ProductivityAction(() => _productivity.SaveSettingAsync("task-template", JsonSerializer.Serialize(SelectedProductivityTask())));
    private async void OnCreateFromTemplate(object sender, RoutedEventArgs e) => await ProductivityAction(async () =>
    {
        var settings = await _productivity.SettingsAsync();
        if (!settings.TryGetValue("task-template", out var json)) throw new ArgumentException("Save a task template first.");
        var t = JsonSerializer.Deserialize<WorkTask>(json)!;
        await _tasks.CreateAsync(t.Title, t.Notes, priority: t.Priority, projectId: t.ProjectId, categoryId: t.CategoryId);
    });
    private async void OnAddRule(object sender, RoutedEventArgs e) => await ProductivityAction(() => _productivity.AddRuleAsync(RuleNameBox.Text,
        ((ComboBoxItem)RuleTriggerBox.SelectedItem).Content.ToString()!, RuleContainsBox.Text, ((ComboBoxItem)RulePriorityBox.SelectedItem).Content.ToString()!));
    private async void OnRunRules(object sender, RoutedEventArgs e) => await ProductivityAction(async () => { await _productivity.RunRulesAsync(DateOnly.FromDateTime(DateTime.Today)); });
    private async void OnDisableRule(object sender, RoutedEventArgs e) => await ProductivityAction(async () =>
    {
        if (RulesBox.SelectedItem is not AutomationRule rule) throw new ArgumentException("Select a rule.");
        await _productivity.DisableRuleAsync(rule.Id);
    });
    private async void OnUndoRule(object sender, RoutedEventArgs e) => await ProductivityAction(async () =>
    {
        if (RuleHistoryGrid.SelectedItem is not AutomationChange change) throw new ArgumentException("Select a history entry.");
        await _productivity.UndoAsync(change.Id);
    });
    private async void OnExportTasks(object sender, RoutedEventArgs e)
    {
        var csv = ((Button)sender).Tag.ToString() == "CSV";
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = csv ? "CSV tasks|*.csv" : "JSON tasks|*.json", FileName = "finance-os-tasks" };
        if (dialog.ShowDialog(this) != true) return;
        await ProductivityAction(async () =>
        {
            ITaskExchangeAdapter adapter = csv ? new CsvTaskExchange() : new JsonTaskExchange();
            await File.WriteAllTextAsync(dialog.FileName, adapter.Export(await AllTasksAsync()));
        });
    }
    private async void OnImportTasks(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Task files|*.json;*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        await ProductivityAction(async () =>
        {
            if (new FileInfo(dialog.FileName).Length > 10 * 1024 * 1024) throw new ArgumentException("Import limit is 10 MB.");
            ITaskExchangeAdapter adapter = Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? new CsvTaskExchange() : new JsonTaskExchange();
            await _productivity.ImportAsync(adapter.Import(await File.ReadAllTextAsync(dialog.FileName)));
        });
    }
}
