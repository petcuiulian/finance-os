using System.Windows;
using FinanceOS.Core;

namespace FinanceOS.Desktop;

public partial class MainWindow : Window, INotificationSink
{
    private readonly IAppStore _store;
    private readonly CancellationTokenSource _lifetime = new();
    public bool SupportsSystemNotifications => false;

    public MainWindow(IAppStore store)
    {
        InitializeComponent();
        _store = store;
        Closed += (_, _) => _lifetime.Cancel();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await UpdateStorageAsync(openSession: true);
    private async void OnCheckStorage(object sender, RoutedEventArgs e) => await UpdateStorageAsync(openSession: false);

    private async Task UpdateStorageAsync(bool openSession)
    {
        CheckStorageButton.IsEnabled = false;
        try
        {
            var status = openSession
                ? await _store.OpenSessionAsync(_lifetime.Token)
                : await _store.ReadStatusAsync(_lifetime.Token);
            StorageText.Text = $"Local SQLite ready · schema {status.SchemaVersion} · workspace launches {status.LaunchCount}";
            CheckStorageButton.IsEnabled = true;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            // Never display SQL, paths, or user data in diagnostic UI. Never reset a failed database.
            StorageText.Text = "Local storage could not be opened. Your existing data has not been reset. Close the app and check folder permissions or use a compatible app version. Preserve the database before recovery.";
        }
    }

    private void OnTestNotice(object sender, RoutedEventArgs e) =>
        Publish(new AppNotice("In-app notification", "The local notification adapter is working. No Windows toast was sent."));

    public void Publish(AppNotice notice)
    {
        Dispatcher.VerifyAccess();
        NoticeText.Text = $"{notice.Title} — {notice.Message}";
    }
}
