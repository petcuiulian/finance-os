using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FinanceOS.Core;
using FinanceOS.Infrastructure;

namespace FinanceOS.Desktop;

public partial class MainWindow : Window, INotificationSink
{
    private readonly IAppStore _store;
    private readonly ITaskStore _tasks;
    private WorkTask? _editing;
    private bool _tasksBusy;
    private readonly CancellationTokenSource _lifetime = new();
    private WorkspacePreferences _preferences = new("Light", "en", "Simple", false);
    private string _page = "Overview";
    private bool _ready;
    private StorageStatus? _lastStatus;
    public bool SupportsSystemNotifications => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);

    public MainWindow(IAppStore store, ITaskStore tasks, SqliteReminderStore reminders, SqliteWorkflowStore workflows)
    {
        InitializeComponent();
        _store = store;
        _tasks = tasks;
        _reminders = reminders;
        _workflows = workflows;
        InitializeReminderLifecycle();
        InitializeWorkflowLifecycle();
        Closed += (_, _) => _lifetime.Cancel();
        Render();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await UpdateStorageAsync(openSession: true);
        if (!CheckStorageButton.IsEnabled) return;
        try
        {
            _preferences = await _store.ReadPreferencesAsync(_lifetime.Token);
            _notificationOptions = await _reminders.ReadOptionsAsync();
            ApplyNotificationOptions();
            _ready = true;
            SetPreferenceButtons(true);
            QuickAddButton.IsEnabled = true;
            SaveTaskButton.IsEnabled = true;
            ApplyTheme();
            Render();
            await _workflows.ReconcileRecurringAsync(DateOnly.FromDateTime(DateTime.Today));
            await RefreshTasksAsync();
            await RefreshWorkflowsAsync();
            await ReconcileRemindersAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            StorageText.Text = "Workspace preferences could not be opened. Preserve the database before recovery.";
        }
    }

    private async void OnCheckStorage(object sender, RoutedEventArgs e) => await UpdateStorageAsync(openSession: false);

    private async Task UpdateStorageAsync(bool openSession)
    {
        CheckStorageButton.IsEnabled = false;
        try
        {
            var status = openSession
                ? await _store.OpenSessionAsync(_lifetime.Token)
                : await _store.ReadStatusAsync(_lifetime.Token);
            _lastStatus = status;
            RenderStatus();
            CheckStorageButton.IsEnabled = true;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            StorageText.Text = "Local storage could not be opened. Your existing data has not been reset. Close the app and check folder permissions or use a compatible app version. Preserve the database before recovery.";
        }
    }

    private async void OnSwitchMode(object sender, RoutedEventArgs e) => await SaveAsync(_preferences with
    {
        Mode = _preferences.Mode == "Simple" ? "Advanced" : "Simple"
    });
    private async void OnSwitchTheme(object sender, RoutedEventArgs e) => await SaveAsync(_preferences with
    {
        Theme = _preferences.Theme == "Light" ? "Dark" : "Light"
    });
    private async void OnSwitchLanguage(object sender, RoutedEventArgs e) => await SaveAsync(_preferences with
    {
        Language = _preferences.Language == "en" ? "ro" : "en"
    });
    private async void OnDismissOnboarding(object sender, RoutedEventArgs e) => await SaveAsync(_preferences with
    {
        OnboardingComplete = true
    });

    private async Task SaveAsync(WorkspacePreferences next)
    {
        if (!_ready) return;
        SetPreferenceButtons(false);
        try
        {
            await _store.SavePreferencesAsync(next, _lifetime.Token);
            _preferences = next;
            ApplyTheme();
            Render();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            NoticeText.Text = "The preference could not be saved. Your previous choice is still active.";
            _page = "Overview";
            RenderPage();
        }
        finally { if (!_lifetime.IsCancellationRequested) SetPreferenceButtons(true); }
    }

    private void SetPreferenceButtons(bool enabled)
    {
        SwitchModeButton.IsEnabled = enabled;
        SwitchThemeButton.IsEnabled = enabled;
        SwitchLanguageButton.IsEnabled = enabled;
        DismissOnboardingButton.IsEnabled = enabled;
    }

    private void ApplyTheme()
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary { Source = new Uri($"Themes/{_preferences.Theme}.xaml", UriKind.Relative) };
    }

    private void OnNavigate(object sender, RoutedEventArgs e)
    {
        _page = (string)((Button)sender).Tag;
        ClearEditor();
        RenderPage();
        _ = RefreshTasksAsync();
        if (_page == "Calendar") _ = RefreshCalendarAsync();
        if (_page == "Reminders") _ = ReconcileRemindersAsync();
        if (_page is "Workflows" or "Waiting") _ = RefreshWorkflowsAsync();
    }

    private void RenderPage()
    {
        RemindersPanel.Visibility = _page == "Reminders" ? Visibility.Visible : Visibility.Collapsed;
        CalendarPanel.Visibility = _page == "Calendar" ? Visibility.Visible : Visibility.Collapsed;
        OverviewPanel.Visibility = _page == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = _page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        WorkflowsPanel.Visibility = _page == "Workflows" ? Visibility.Visible : Visibility.Collapsed;
        WaitingPanel.Visibility = _page == "Waiting" ? Visibility.Visible : Visibility.Collapsed;
        TaskPanel.Visibility = _page is "Tasks" or "MyDay" or "Archive" or "Trash" ? Visibility.Visible : Visibility.Collapsed;
        TaskHeading.Text = T(_page, _page switch { "MyDay" => "Ziua mea", "Tasks" => "Sarcini", "Archive" => "Arhivă", "Trash" => "Coș", _ => _page });
        PageTitle.Text = T(_page, _page == "Overview" ? "Prezentare" : "Setări");
        OverviewButton.SetResourceReference(Button.BackgroundProperty, _page == "Overview" ? "AccentSubtleBrush" : "SurfaceBrush");
        SettingsButton.SetResourceReference(Button.BackgroundProperty, _page == "Settings" ? "AccentSubtleBrush" : "SurfaceBrush");
        MyDayButton.SetResourceReference(Button.BackgroundProperty, _page == "MyDay" ? "AccentSubtleBrush" : "SurfaceBrush");
        TasksButton.SetResourceReference(Button.BackgroundProperty, _page == "Tasks" ? "AccentSubtleBrush" : "SurfaceBrush");
        ArchiveButton.SetResourceReference(Button.BackgroundProperty, _page == "Archive" ? "AccentSubtleBrush" : "SurfaceBrush");
        TrashButton.SetResourceReference(Button.BackgroundProperty, _page == "Trash" ? "AccentSubtleBrush" : "SurfaceBrush");
        WorkflowsButton.SetResourceReference(Button.BackgroundProperty, _page == "Workflows" ? "AccentSubtleBrush" : "SurfaceBrush");
        WaitingButton.SetResourceReference(Button.BackgroundProperty, _page == "Waiting" ? "AccentSubtleBrush" : "SurfaceBrush");
    }

    private string T(string english, string romanian) => _preferences.Language == "ro" ? romanian : english;

    private void Render()
    {
        RenderPage();
        NavigateLabel.Text = T("WORKSPACE", "SPAȚIU DE LUCRU");
        ComingSoonLabel.Text = T("COMING LATER", "ÎN CURÂND");
        OverviewButton.Content = T("_Overview", "_Prezentare");
        SettingsButton.Content = T("_Settings", "_Setări");
        MyDayButton.Content = T("_My Day", "_Ziua mea");
        TasksButton.Content = T("_Tasks", "_Sarcini");
        ArchiveButton.Content = T("_Archive", "_Arhivă");
        TrashButton.Content = T("T_rash", "_Coș");
        WorkflowsButton.Content = T("Workflows", "Fluxuri de lucru");
        WaitingButton.Content = T("Waiting", "În așteptare");
        DeleteTaskButton.Content = T("Move to _trash…", "Mută în _coș…");
        AdvancedFieldsPanel.Visibility = _preferences.Mode == "Advanced" ? Visibility.Visible : Visibility.Collapsed;
        CalendarButton.Content = T("Calendar", "Calendar");
        RemindersButton.Content = T("Reminders", "Mementouri");
        WorkflowsButton.Content = T("Workflows · M4", "Fluxuri de lucru · M4");
        ProjectsButton.Content = T("Projects · M4", "Proiecte · M4");
        AnalyticsButton.Content = T("Analytics · M5", "Analiză · M5");
        SwitchModeButton.Content = _preferences.Mode == "Simple" ? T("Simple mode", "Mod simplu") : T("Advanced mode", "Mod avansat");
        SwitchThemeButton.Content = _preferences.Theme == "Light" ? T("Dark theme", "Temă închisă") : T("Light theme", "Temă deschisă");
        SwitchLanguageButton.Content = _preferences.Language == "en" ? "RO" : "EN";
        OnboardingPanel.Visibility = _preferences.OnboardingComplete ? Visibility.Collapsed : Visibility.Visible;
        WelcomeTitle.Text = T("Welcome to your private workspace", "Bine ai venit în spațiul tău privat");
        WelcomeBody.Text = T("Your settings are saved on this device. Tasks and planning arrive in the next milestones.", "Preferințele sunt salvate pe acest dispozitiv. Sarcinile și planificarea vor fi disponibile în etapele următoare.");
        DismissOnboardingButton.Content = T("_Get started", "_Începe");
        OverviewHeading.Text = T("A clear start to the day.", "Un început clar pentru fiecare zi.");
        OverviewDescription.Text = T("Capture tasks quickly. Your work is stored locally and survives restart.", "Adaugă sarcini rapid. Munca ta este salvată local și rămâne disponibilă după repornire.");
        StorageHeading.Text = T("Local storage", "Stocare locală");
        PrivacyText.Text = T("Data stays in your Windows user profile.", "Datele rămân în profilul tău Windows.");
        CheckStorageButton.Content = T("_Check local storage", "_Verifică stocarea locală");
        TestNoticeButton.Content = T("Test _in-app notice", "Testează _notificarea în aplicație");
        SettingsHeading.Text = T("Make the workspace yours.", "Configurează-ți spațiul de lucru.");
        SettingsDescription.Text = T("These choices persist locally and apply to the same workspace.", "Aceste opțiuni sunt păstrate local și se aplică aceluiași spațiu de lucru.");
        PreferencesHeading.Text = T("Preferences", "Preferințe");
        ModeDescription.Text = T("Simple and Advanced share the same data. Advanced features appear as they are built.", "Modurile Simplu și Avansat folosesc aceleași date. Funcțiile avansate vor apărea pe măsură ce sunt construite.");
        UnavailableDescription.Text = T("Appearance, language and mode can be changed from the header.", "Aspectul, limba și modul pot fi schimbate din bara de sus.");
        CurrentPreferencesText.Text = T($"Theme: {_preferences.Theme} · Language: {_preferences.Language.ToUpperInvariant()} · Mode: {_preferences.Mode}",
            $"Temă: {_preferences.Theme} · Limbă: {_preferences.Language.ToUpperInvariant()} · Mod: {_preferences.Mode}");
        if (_lastStatus is not null) RenderStatus();
        if (NoticeText.Text.Contains("M3", StringComparison.Ordinal))
            NoticeText.Text = T("Enable Windows notifications in Reminders. Keep the app open or in the tray; closing it stops live delivery.", "Activează notificările Windows în Mementouri. Aplicația trebuie să rămână deschisă sau în tray.");
        QuickAddButton.Content = T("_Add task", "_Adaugă sarcină");
        EditorHeading.Text = _editing is null ? T("New task", "Sarcină nouă") : T("Edit task", "Editează sarcina");
        if (!_ready) SetPreferenceButtons(false);
    }

    private int _queryGeneration;

    private async Task RefreshTasksAsync()
    {
        if (!_ready) return;
        var generation = ++_queryGeneration;
        try
        {
            var view = _page is "MyDay" or "Tasks" or "Archive" ? _page : "Overview";
            var filter = view == "Tasks" ? (TaskFilterBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All" : "All";
            var items = await _tasks.ListAsync(new TaskQuery(view, SearchBox.Text, filter), _lifetime.Token);
            if (generation != _queryGeneration || _lifetime.IsCancellationRequested) return;
            TaskGrid.ItemsSource = items;
            TaskSummaryText.Text = T($"{items.Count} task(s) in this view", $"{items.Count} sarcini în această vizualizare");
            if (_page == "Overview")
            {
                var open = items.Count(x => x.Status == "Open");
                var due = items.Count(x => x.Status == "Open" && x.DueDate <= DateOnly.FromDateTime(DateTime.Today));
                OverviewDescription.Text = T($"{open} open · {due} due or overdue. Your work is stored locally.",
                    $"{open} deschise · {due} scadente. Munca ta este salvată local.");
            }
            UpdateTaskActions();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception) { TaskErrorText.Text = T("Tasks could not be loaded. Existing data was preserved.", "Sarcinile nu au putut fi încărcate. Datele existente au fost păstrate."); }
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e) => await RefreshTasksAsync();
    private async void OnFilterChanged(object sender, SelectionChangedEventArgs e) => await RefreshTasksAsync();
    private void OnTaskSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateTaskActions();

    private void UpdateTaskActions()
    {
        var task = TaskGrid.SelectedItem as WorkTask;
        CompleteTaskButton.IsEnabled = !_tasksBusy && task?.Status == "Open";
        ReopenTaskButton.IsEnabled = !_tasksBusy && task is { Status: "Done" or "Archived" or "Deleted" };
        ReopenTaskButton.Content = task?.Status == "Deleted" ? T("_Restore", "_Restaurează") : T("_Reopen", "_Redeschide");
        ArchiveTaskButton.IsEnabled = !_tasksBusy && task is { Status: "Open" or "Done" };
        DeleteTaskButton.IsEnabled = !_tasksBusy && task is not null && task.Status != "Deleted";
        if (task is null) return;
        _editing = task;
        _reminderTask = task;
        _workflowSelectedTask = task;
        TaskTitleBox.Text = task.Title;
        TaskNotesBox.Text = task.Notes;
        TaskDuePicker.SelectedDate = task.DueDate?.ToDateTime(TimeOnly.MinValue);
        TaskPriorityBox.SelectedIndex = task.Priority switch { "Low" => 0, "High" => 2, "Urgent" => 3, _ => 1 };
        EditorHeading.Text = T("Edit task", "Editează sarcina");
    }

    private void OnCancelEdit(object sender, RoutedEventArgs e) => ClearEditor();
    private void ClearEditor()
    {
        _editing = null;
        TaskGrid.SelectedItem = null;
        TaskTitleBox.Clear();
        TaskNotesBox.Clear();
        TaskDuePicker.SelectedDate = null;
        TaskPriorityBox.SelectedIndex = 1;
        EditorHeading.Text = T("New task", "Sarcină nouă");
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            QuickTitleBox.Focus();
            QuickTitleBox.SelectAll();
            e.Handled = true;
        }
    }

    private async void OnQuickTitleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await AddQuickAsync();
    }
    private async void OnQuickAdd(object sender, RoutedEventArgs e) => await AddQuickAsync();
    private async Task AddQuickAsync()
    {
        var title = QuickTitleBox.Text.Trim();
        if (title.Length == 0) { QuickTitleBox.Focus(); return; }
        await RunTaskActionAsync(async () =>
        {
            await _tasks.CreateAsync(title, cancellationToken: _lifetime.Token);
            QuickTitleBox.Clear();
        });
    }

    private async void OnSaveTask(object sender, RoutedEventArgs e)
    {
        var title = TaskTitleBox.Text.Trim();
        if (title.Length == 0) { TaskTitleBox.Focus(); return; }
        await RunTaskActionAsync(async () =>
        {
            var advanced = _preferences.Mode == "Advanced";
            var notes = advanced ? TaskNotesBox.Text : _editing?.Notes ?? "";
            var due = advanced ? (TaskDuePicker.SelectedDate is DateTime date ? DateOnly.FromDateTime(date) : null) : _editing?.DueDate;
            var priority = advanced ? (TaskPriorityBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal" : _editing?.Priority ?? "Normal";
            if (_editing is null)
                await _tasks.CreateAsync(title, notes, due, priority, cancellationToken: _lifetime.Token);
            else
                await _tasks.UpdateAsync(_editing with { Title = title, Notes = notes, DueDate = due, Priority = priority }, _lifetime.Token);
            ClearEditor();
        });
    }

    private async void OnCompleteTask(object sender, RoutedEventArgs e) => await ChangeStatusAsync("Done");
    private async void OnReopenTask(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is WorkTask { Status: "Deleted" } selected)
            await RunTaskActionAsync(async () => { await _tasks.RestoreAsync(selected.Id, _lifetime.Token); ClearEditor(); });
        else await ChangeStatusAsync("Open");
    }
    private async void OnArchiveTask(object sender, RoutedEventArgs e) => await ChangeStatusAsync("Archived");
    private async Task ChangeStatusAsync(string status)
    {
        if (TaskGrid.SelectedItem is not WorkTask selected) return;
        await RunTaskActionAsync(async () =>
        {
            await _tasks.SetStatusAsync(selected.Id, status, _lifetime.Token);
            ClearEditor();
        });
    }
    private async void OnDeleteTask(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not WorkTask selected) return;
        if (MessageBox.Show(this, T($"Move '{selected.Title}' to Trash? You can restore it later.", $"Muți '{selected.Title}' în Coș? O poți restaura ulterior."),
            T("Move to Trash", "Mută în Coș"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunTaskActionAsync(async () =>
        {
            await _tasks.DeleteAsync(selected.Id, _lifetime.Token);
            ClearEditor();
        });
    }
    private async Task RunTaskActionAsync(Func<Task> action)
    {
        if (!_ready || _tasksBusy) return;
        _tasksBusy = true;
        SaveTaskButton.IsEnabled = false;
        QuickAddButton.IsEnabled = false;
        UpdateTaskActions();
        TaskErrorText.Text = "";
        try { await action(); await RefreshTasksAsync(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (ArgumentException ex) { TaskErrorText.Text = ex.Message; }
        catch (InvalidOperationException ex) { TaskErrorText.Text = ex.Message; }
        catch (Exception) { TaskErrorText.Text = T("Task change failed. Existing data was preserved.", "Modificarea sarcinii a eșuat. Datele existente au fost păstrate."); }
        finally
        {
            _tasksBusy = false;
            SaveTaskButton.IsEnabled = true;
            QuickAddButton.IsEnabled = true;
            UpdateTaskActions();
        }
    }

    private void RenderStatus()
    {
        if (_lastStatus is null) return;
        StorageText.Text = T($"Local SQLite ready · schema {_lastStatus.SchemaVersion} · workspace launches {_lastStatus.LaunchCount}",
            $"SQLite local pregătit · schema {_lastStatus.SchemaVersion} · porniri {_lastStatus.LaunchCount}");
    }

    private void OnTestNotice(object sender, RoutedEventArgs e) =>
        Publish(new AppNotice(T("In-app notification", "Notificare în aplicație"),
            T("The local notification adapter is working. No Windows toast was sent.", "Notificarea locală funcționează. Nu a fost trimisă o notificare Windows.")));

    public void Publish(AppNotice notice)
    {
        Dispatcher.VerifyAccess();
        NoticeText.Text = $"{notice.Title} — {notice.Message}";
    }
}
