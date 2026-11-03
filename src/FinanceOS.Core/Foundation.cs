namespace FinanceOS.Core;

public sealed record StorageStatus(int SchemaVersion, long LaunchCount);

public sealed record WorkspacePreferences(string Theme, string Language, string Mode, bool OnboardingComplete);

public interface IAppStore
{
    Task<StorageStatus> OpenSessionAsync(CancellationToken cancellationToken = default);
    Task<WorkspacePreferences> ReadPreferencesAsync(CancellationToken cancellationToken = default);
    Task SavePreferencesAsync(WorkspacePreferences preferences, CancellationToken cancellationToken = default);
    Task<StorageStatus> ReadStatusAsync(CancellationToken cancellationToken = default);
}

// Delivery capability is explicit: M0 implements in-app notices only. OS delivery,
// persisted reminders and reconciliation belong to M3, not a timer in a view.
public sealed record AppNotice(string Title, string Message);

public interface INotificationSink
{
    bool SupportsSystemNotifications { get; }
    void Publish(AppNotice notice);
}

public static class AppDataPaths
{
    public static string DatabasePath(string localApplicationData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);
        if (!Path.IsPathFullyQualified(localApplicationData))
            throw new ArgumentException("An absolute per-user data directory is required.", nameof(localApplicationData));
        return Path.Combine(localApplicationData, "FinanceOS", "finance-os.db");
    }
}
