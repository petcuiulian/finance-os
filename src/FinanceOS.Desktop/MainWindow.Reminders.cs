using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FinanceOS.Core;
using FinanceOS.Infrastructure;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;

namespace FinanceOS.Desktop;

public partial class MainWindow
{
    private readonly SqliteReminderStore _reminders;
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _reconciling;
    private WorkTask? _reminderTask;

    private NotificationOptions _notificationOptions = new(false, false, false, false);
    private bool _loadingNotificationOptions = true;

    private void ApplyNotificationOptions()
    {
        _loadingNotificationOptions = true;
        EnableToastsBox.IsChecked = _notificationOptions.Toasts;
        EnableSoundBox.IsChecked = _notificationOptions.Sound;
        WorkingHoursOnlyBox.IsChecked = _notificationOptions.WorkingHoursOnly;
        WeekdaysOnlyBox.IsChecked = _notificationOptions.WeekdaysOnly;
        _loadingNotificationOptions = false;
    }

    private async void OnNotificationOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingNotificationOptions || !_ready) return;
        NotificationOptionsPanel.IsEnabled = false;
        try
        {
            var options = new NotificationOptions(EnableToastsBox.IsChecked == true, EnableSoundBox.IsChecked == true,
                WorkingHoursOnlyBox.IsChecked == true, WeekdaysOnlyBox.IsChecked == true);
            await _reminders.SaveOptionsAsync(options);
            _notificationOptions = options;
            await ReconcileRemindersAsync();
        }
        catch (Exception) { ApplyNotificationOptions(); ReminderStatusText.Text = "Notification preferences could not be saved."; }
        finally { NotificationOptionsPanel.IsEnabled = true; }
    }

    private void InitializeReminderLifecycle()
    {
        ReminderDatePicker.SelectedDate = DateTime.Today;
        CalendarDatePicker.SelectedDate = DateTime.Today;
        ReminderZoneText.Text = $"Time zone: {TimeZoneInfo.Local.Id}. Ambiguous times use the later occurrence.";
        _reminderTimer.Tick += async (_, _) => await ReconcileRemindersAsync();
        _reminderTimer.Start();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Closed += (_, _) =>
        {
            _reminderTimer.Stop();
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _tray?.Dispose();
        };
    }

    public void OpenReminderInbox()
    {
        _page = "Reminders"; RenderPage(); _ = ReconcileRemindersAsync();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(async () => await ReconcileRemindersAsync());
    }

    private async Task ReconcileRemindersAsync()
    {
        if (!_ready || _reconciling || _lifetime.IsCancellationRequested) return;
        _reconciling = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            await _reminders.ReconcileAsync(now);
            var alerts = await _reminders.AlertsAsync(now);
            if (_lifetime.IsCancellationRequested) return;
            ReminderGrid.ItemsSource = alerts;
            ScheduledReminderGrid.ItemsSource = await _reminders.SchedulesAsync();
            RemindersButton.Content = T($"Reminders ({alerts.Count})", $"Mementouri ({alerts.Count})");
            ReminderStatusText.Text = T($"{alerts.Count} active alerts. Recovered alerts stay until dismissed.", $"{alerts.Count} alerte active. Alertele recuperate rămân până la confirmare.");
            if (EnableToastsBox.IsChecked != true) return;
            var local = DateTime.Now;
            if (WorkingHoursOnlyBox.IsChecked == true && (local.Hour < 9 || local.Hour >= 17)) return;
            if (WeekdaysOnlyBox.IsChecked == true && local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return;
            foreach (var alert in alerts)
            {
                if (!await _reminders.ClaimToastAsync(alert.Id, now)) continue;
                try
                {
                    var toast = new ToastContentBuilder().AddText("FINANCE OS").AddText(alert.Title)
                        .AddText("Open FINANCE OS to snooze, dismiss or view the task.");
                    if (EnableSoundBox.IsChecked != true) toast.AddAudio(new Uri("ms-winsoundevent:Notification.Default"), silent: true);
                    toast.Show(notification => { notification.Tag = alert.Id[..16]; notification.Group = "reminders"; });
                }
                catch (Exception)
                {
                    ReminderStatusText.Text = "Windows toast is unavailable or blocked. Your alerts remain in this inbox.";
                }
            }
        }
        catch (Exception)
        {
            if (!_lifetime.IsCancellationRequested) ReminderStatusText.Text = "Reminder reconciliation failed. It will retry; existing reminders were preserved.";
        }
        finally { _reconciling = false; }
    }

    private static DateTime LocalInput(DateTime? date, string time)
    {
        if (date is null || !TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw new ArgumentException("Choose a date and a time in HH:mm format.");
        return DateTime.SpecifyKind(date.Value.Date.Add(parsed.ToTimeSpan()), DateTimeKind.Unspecified);
    }

    private async void OnAddReminder(object sender, RoutedEventArgs e)
    {
        if (_reminderTask is null) { ReminderStatusText.Text = "Select an open task in Tasks first."; return; }
        AddReminderButton.IsEnabled = false;
        try
        {
            if (!int.TryParse(IntervalMinutesBox.Text, out var interval)) throw new ArgumentException("Enter an interval in minutes.");
            await _reminders.AddAsync(new ReminderDraft(_reminderTask.Id, LocalInput(ReminderDatePicker.SelectedDate, ReminderTimeBox.Text),
                TimeZoneInfo.Local.Id, ((ComboBoxItem)RecurrenceBox.SelectedItem).Content.ToString()!, interval));
            await ReconcileRemindersAsync();
            ReminderStatusText.Text = "Reminder saved. Past times are recovered in the inbox.";
        }
        catch (ArgumentException ex) { ReminderStatusText.Text = ex.Message; }
        catch (Exception) { ReminderStatusText.Text = "Reminder could not be saved. Existing data was preserved."; }
        finally { AddReminderButton.IsEnabled = true; }
    }

    private async void OnCancelSchedule(object sender, RoutedEventArgs e)
    {
        if (ScheduledReminderGrid.SelectedItem is not ReminderScheduleEntry entry) return;
        try { await _reminders.CancelScheduleAsync(entry.Id); await ReconcileRemindersAsync(); }
        catch (Exception) { ReminderStatusText.Text = "Cancellation failed. The schedule has been preserved."; }
    }

    private async void OnDismissReminder(object sender, RoutedEventArgs e)
    {
        if (ReminderGrid.SelectedItem is not ReminderAlert alert) return;
        try { await _reminders.DismissAsync(alert.Id); await ReconcileRemindersAsync(); }
        catch (Exception) { ReminderStatusText.Text = "Dismiss failed. The alert is still available."; }
    }

    private async void OnSnoozeReminder(object sender, RoutedEventArgs e)
    {
        if (ReminderGrid.SelectedItem is not ReminderAlert alert) return;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var choice = ((ComboBoxItem)SnoozeMinutesBox.SelectedItem).Content.ToString();
            var until = choice switch
            {
                "Tomorrow" => ReminderSchedule.ToUtc(DateTime.Today.AddDays(1).AddHours(9), TimeZoneInfo.Local.Id),
                "Custom" => ReminderSchedule.ToUtc(LocalInput(ReminderDatePicker.SelectedDate, ReminderTimeBox.Text), TimeZoneInfo.Local.Id),
                _ => now.AddMinutes(int.Parse(choice!, CultureInfo.InvariantCulture))
            };
            await _reminders.SnoozeAsync(alert.Id, until, now);
            await ReconcileRemindersAsync();
        }
        catch (ArgumentException ex) { ReminderStatusText.Text = ex.Message; }
        catch (Exception) { ReminderStatusText.Text = "Snooze failed. The alert remains in the inbox."; }
    }

    private async void OnOpenReminderTask(object sender, RoutedEventArgs e)
    {
        if (ReminderGrid.SelectedItem is not ReminderAlert alert) return;
        _page = "Tasks"; SearchBox.Clear(); TaskFilterBox.SelectedIndex = 0; RenderPage();
        await RefreshTasksAsync();
        if (TaskGrid.ItemsSource is IEnumerable<WorkTask> tasks)
        {
            TaskGrid.SelectedItem = tasks.FirstOrDefault(x => x.Id == alert.TaskId);
            if (TaskGrid.SelectedItem is not null) TaskGrid.ScrollIntoView(TaskGrid.SelectedItem);
        }
    }

    private void OnMinimizeToTray(object sender, RoutedEventArgs e)
    {
        if (_tray is null)
        {
            _tray = new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "FINANCE OS — reminders active" };
            _tray.DoubleClick += (_, _) => RestoreFromTray();
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open FINANCE OS", null, (_, _) => RestoreFromTray());
            menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(Close));
            _tray.ContextMenuStrip = menu;
        }
        _tray.Visible = true; Hide();
    }

    private void RestoreFromTray() => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); if (_tray is not null) _tray.Visible = false; });

    private async void OnCalendarChanged(object? sender, SelectionChangedEventArgs e) => await RefreshCalendarAsync();
    private async void OnCalendarViewChanged(object sender, SelectionChangedEventArgs e) => await RefreshCalendarAsync();
    private int _calendarGeneration;
    private async Task RefreshCalendarAsync()
    {
        if (!_ready || CalendarDatePicker.SelectedDate is null || CalendarViewBox.SelectedItem is not ComboBoxItem view) return;
        var generation = ++_calendarGeneration;
        try
        {
            var start = CalendarDatePicker.SelectedDate.Value.Date;
            var end = start.AddDays(1);
            switch (view.Content.ToString())
            {
                case "Week": start = start.AddDays(-((int)start.DayOfWeek + 6) % 7); end = start.AddDays(7); break;
                case "Month": start = new DateTime(start.Year, start.Month, 1); end = start.AddMonths(1); break;
                case "Agenda": end = start.AddDays(90); break;
            }
            var entries = await _reminders.CalendarAsync(start, end, TimeZoneInfo.Local.Id);
            if (generation != _calendarGeneration) return;
            CalendarGrid.ItemsSource = entries;
            CalendarStatusText.Text = $"{entries.Count} entries · {start:d} — {end.AddDays(-1):d} · {TimeZoneInfo.Local.Id}";
        }
        catch (Exception) { CalendarStatusText.Text = "Calendar could not be loaded. Your data was preserved."; }
    }

    private async void OnAddBlock(object sender, RoutedEventArgs e)
    {
        AddBlockButton.IsEnabled = false;
        try
        {
            await _reminders.AddBlockAsync(BlockTitleBox.Text, LocalInput(CalendarDatePicker.SelectedDate, BlockStartBox.Text),
                LocalInput(CalendarDatePicker.SelectedDate, BlockEndBox.Text), TimeZoneInfo.Local.Id);
            BlockTitleBox.Clear(); await RefreshCalendarAsync();
        }
        catch (ArgumentException ex) { CalendarStatusText.Text = ex.Message; }
        catch (Exception) { CalendarStatusText.Text = "Time block could not be saved."; }
        finally { AddBlockButton.IsEnabled = true; }
    }

    private async void OnRemoveBlock(object sender, RoutedEventArgs e)
    {
        if (CalendarGrid.SelectedItem is not CalendarEntry { Kind: "Time block" } entry) return;
        if (MessageBox.Show(this, "Remove this time block?", "FINANCE OS", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { await _reminders.DeleteBlockAsync(entry.Id); await RefreshCalendarAsync(); }
        catch (Exception) { CalendarStatusText.Text = "Time block could not be removed."; }
    }
}
