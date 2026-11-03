using System.Windows;
using System.Windows.Controls;
using FinanceOS.Core;

namespace FinanceOS.Desktop;

public partial class MainWindow : Window, INotificationSink
{
    private readonly IAppStore _store;
    private readonly CancellationTokenSource _lifetime = new();
    private WorkspacePreferences _preferences = new("Light", "en", "Simple", false);
    private string _page = "Overview";
    private bool _ready;
    private StorageStatus? _lastStatus;
    public bool SupportsSystemNotifications => false;

    public MainWindow(IAppStore store)
    {
        InitializeComponent();
        _store = store;
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
            _ready = true;
            ApplyTheme();
            Render();
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
        RenderPage();
    }

    private void RenderPage()
    {
        OverviewPanel.Visibility = _page == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = _page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = T(_page, _page == "Overview" ? "Prezentare" : "Setări");
    }

    private string T(string english, string romanian) => _preferences.Language == "ro" ? romanian : english;

    private void Render()
    {
        RenderPage();
        NavigateLabel.Text = T("WORKSPACE", "SPAȚIU DE LUCRU");
        ComingSoonLabel.Text = T("COMING LATER", "ÎN CURÂND");
        OverviewButton.Content = T("_Overview", "_Prezentare");
        SettingsButton.Content = T("_Settings", "_Setări");
        MyDayButton.Content = T("My Day · M2", "Ziua mea · M2");
        TasksButton.Content = T("Tasks · M2", "Sarcini · M2");
        CalendarButton.Content = T("Calendar · M3", "Calendar · M3");
        RemindersButton.Content = T("Reminders · M3", "Mementouri · M3");
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
        OverviewDescription.Text = T("Your offline workspace is ready. Tasks, calendars and reminders will arrive in upcoming milestones.", "Spațiul tău offline este pregătit. Sarcinile, calendarul și mementourile vor fi disponibile în etapele următoare.");
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
            NoticeText.Text = T("System notifications and scheduling arrive in M3. This app does not run after exit.", "Notificările de sistem și programarea vor fi disponibile în M3. Aplicația nu rulează după închidere.");
        if (!_ready) SetPreferenceButtons(false);
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
