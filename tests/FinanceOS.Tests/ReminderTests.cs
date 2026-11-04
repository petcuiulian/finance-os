using FinanceOS.Core;
using FinanceOS.Infrastructure;
using Xunit;

namespace FinanceOS.Tests;

public sealed class ReminderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "finance-reminder-tests", Guid.NewGuid().ToString("N"));
    private string Database => AppDataPaths.DatabasePath(_root);
    private SqliteReminderStore Store => new(Database);
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private async Task<WorkTask> SetupAsync()
    {
        await new SqliteAppStore(Database).OpenSessionAsync();
        return await new SqliteTaskStore(Database).CreateAsync("Synthetic reminder task", dueDate: DateOnly.FromDateTime(Now.Date));
    }

    [Fact]
    public async Task MissedReminderRecoversOnceAcrossConcurrentReconciliationAndReopen()
    {
        var task = await SetupAsync();
        await Store.AddAsync(new ReminderDraft(task.Id, Now.UtcDateTime.AddHours(-3), "UTC"));
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Store.ReconcileAsync(Now)));
        var alert = Assert.Single(await Store.AlertsAsync(Now));
        Assert.Equal(task.Id, alert.TaskId);
        var claims = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Store.ClaimToastAsync(alert.Id, Now)));
        Assert.Single(claims, x => x);
        Assert.Single(await new SqliteReminderStore(Database).AlertsAsync(Now));
        await Store.ReconcileAsync(Now.AddDays(1));
        Assert.Single(await Store.AlertsAsync(Now.AddDays(1)));
    }

    [Fact]
    public async Task SnoozeAndDismissSurviveRestart()
    {
        var task = await SetupAsync();
        await Store.AddAsync(new ReminderDraft(task.Id, Now.UtcDateTime, "UTC"));
        await Store.ReconcileAsync(Now);
        var alert = Assert.Single(await Store.AlertsAsync(Now));
        Assert.True(await Store.ClaimToastAsync(alert.Id, Now));
        await Store.SnoozeAsync(alert.Id, Now.AddMinutes(10), Now);
        Assert.Empty(await Store.AlertsAsync(Now.AddMinutes(9)));
        Assert.Single(await Store.AlertsAsync(Now.AddMinutes(10)));
        Assert.True(await Store.ClaimToastAsync(alert.Id, Now.AddMinutes(10)));
        await Store.DismissAsync(alert.Id);
        Assert.Empty(await new SqliteReminderStore(Database).AlertsAsync(Now.AddDays(1)));
    }

    [Fact]
    public async Task CompletionAndTrashSuppressDelivery()
    {
        var task = await SetupAsync();
        await Store.AddAsync(new ReminderDraft(task.Id, Now.UtcDateTime, "UTC"));
        await new SqliteTaskStore(Database).SetStatusAsync(task.Id, "Done");
        await Store.ReconcileAsync(Now);
        Assert.Empty(await Store.AlertsAsync(Now));
        await new SqliteTaskStore(Database).SetStatusAsync(task.Id, "Open");
        await Store.ReconcileAsync(Now);
        Assert.Single(await Store.AlertsAsync(Now));
        await new SqliteTaskStore(Database).DeleteAsync(task.Id);
        Assert.Empty(await Store.AlertsAsync(Now));
    }

    [Fact]
    public async Task RecurrenceCoalescesMissedPeriodsAndAdvancesToFuture()
    {
        var task = await SetupAsync();
        await Store.AddAsync(new ReminderDraft(task.Id, Now.UtcDateTime.AddDays(-20), "UTC", "Daily"));
        await Store.ReconcileAsync(Now);
        Assert.Single(await Store.AlertsAsync(Now));
        await Store.ReconcileAsync(Now.AddHours(23));
        Assert.Single(await Store.AlertsAsync(Now.AddHours(23)));
        await Store.ReconcileAsync(Now.AddDays(1));
        Assert.Equal(2, (await Store.AlertsAsync(Now.AddDays(1))).Count);
    }

    [Fact]
    public async Task ConsentPersistsAndCancellationStopsFutureOccurrences()
    {
        var task = await SetupAsync();
        var options = new NotificationOptions(true, false, true, true);
        await Store.SaveOptionsAsync(options);
        Assert.Equal(options, await new SqliteReminderStore(Database).ReadOptionsAsync());
        await Store.AddAsync(new ReminderDraft(task.Id, Now.UtcDateTime, "UTC", "Daily"));
        var schedule = Assert.Single(await Store.SchedulesAsync());
        await Store.ReconcileAsync(Now);
        var alert = Assert.Single(await Store.AlertsAsync(Now));
        await Store.CancelScheduleAsync(schedule.Id);
        Assert.Empty(await Store.SchedulesAsync());
        Assert.Empty(await Store.AlertsAsync(Now));
        Assert.False(await Store.ClaimToastAsync(alert.Id, Now));
        await Store.ReconcileAsync(Now.AddDays(4));
        Assert.Empty(await Store.AlertsAsync(Now.AddDays(4)));
    }

    [Fact]
    public async Task SnoozedOrCompletedAlertCannotBeClaimedForToast()
    {
        var task = await SetupAsync();
        await Store.AddAsync(new ReminderDraft(task.Id, Now.UtcDateTime, "UTC"));
        await Store.ReconcileAsync(Now);
        var alert = Assert.Single(await Store.AlertsAsync(Now));
        await Store.SnoozeAsync(alert.Id, Now.AddMinutes(5), Now);
        Assert.False(await Store.ClaimToastAsync(alert.Id, Now));
        await new SqliteTaskStore(Database).SetStatusAsync(task.Id, "Done");
        Assert.False(await Store.ClaimToastAsync(alert.Id, Now.AddMinutes(5)));
    }

    [Fact]
    public void DstGapRejectedAndAmbiguousTimeChoosesLaterInstant()
    {
        Assert.Throws<ArgumentException>(() => ReminderSchedule.ToUtc(new DateTime(2026, 3, 29, 3, 30, 0), "Europe/Bucharest"));
        var ambiguous = ReminderSchedule.ToUtc(new DateTime(2026, 10, 25, 3, 30, 0), "Europe/Bucharest");
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), ambiguous);
        var draft = new ReminderDraft("test", new DateTime(2026, 3, 28, 3, 30, 0), "Europe/Bucharest", "Daily");
        var next = ReminderSchedule.NextAfter(draft, ReminderSchedule.ToUtc(draft.LocalTime, draft.TimeZoneId));
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void MonthlyRecurrenceKeepsOriginalDayAndIntervalIsInstantBased()
    {
        var draft = new ReminderDraft("test", new DateTime(2026, 1, 31, 9, 0, 0), "UTC", "Monthly");
        Assert.Equal(new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero), ReminderSchedule.NextAfter(draft, new DateTimeOffset(draft.LocalTime, TimeSpan.Zero)));
        Assert.Equal(new DateTimeOffset(2026, 3, 31, 9, 0, 0, TimeSpan.Zero), ReminderSchedule.NextAfter(draft, new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero)));
        var interval = draft with { Recurrence = "Interval", IntervalMinutes = 15 };
        Assert.Equal(Now.AddMinutes(15), ReminderSchedule.NextAfter(interval, Now));
    }

    [Fact]
    public async Task CalendarIncludesOverlappingBlocksAndLocalDeadlines()
    {
        await SetupAsync();
        await Store.AddBlockAsync("Focus", Now.Date.AddHours(9), Now.Date.AddHours(11), "UTC");
        var entries = await Store.CalendarAsync(Now.Date, Now.Date.AddDays(1), "UTC");
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, x => x.Kind == "Deadline");
        var block = Assert.Single(entries, x => x.Kind == "Time block");
        Assert.Single(await Store.CalendarAsync(Now.Date.AddHours(10), Now.Date.AddHours(12), "UTC"));
        await Store.DeleteBlockAsync(block.Id);
        Assert.Single(await Store.CalendarAsync(Now.Date, Now.Date.AddDays(1), "UTC"));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.AddBlockAsync("Invalid", Now.Date, Now.Date, "UTC"));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
