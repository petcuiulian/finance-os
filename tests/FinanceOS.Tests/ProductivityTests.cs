using FinanceOS.Core;
using FinanceOS.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FinanceOS.Tests;

public sealed class ProductivityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "finance-productivity", Guid.NewGuid().ToString("N"));
    private string PathName => AppDataPaths.DatabasePath(_root);
    private SqliteProductivityStore Store => new(PathName);
    private SqliteTaskStore Tasks => new(PathName);
    private async Task OpenAsync() => await new SqliteAppStore(PathName).OpenSessionAsync();
    [Fact]
    public async Task RulesAreDeterministicIdempotentAndReversible()
    {
        await OpenAsync(); var today = new DateOnly(2026, 11, 4);
        var task = await Tasks.CreateAsync("Synthetic invoice", dueDate: today.AddDays(-1));
        await Store.AddRuleAsync("First", "Overdue", "invoice", "Urgent");
        await Store.AddRuleAsync("Conflicting", "Overdue", "", "Low");
        Assert.Equal(1, await Store.RunRulesAsync(today));
        Assert.Equal(0, await Store.RunRulesAsync(today));
        Assert.Equal("Urgent", Assert.Single(await Tasks.ListAsync(new TaskQuery("Tasks"))).Priority);
        await Store.UndoAsync(Assert.Single(await Store.HistoryAsync()).Id);
        Assert.Equal("Normal", Assert.Single(await Tasks.ListAsync(new TaskQuery("Tasks"))).Priority);
        Assert.Equal(0, await Store.RunRulesAsync(today));
        Assert.True(Assert.Single(await Store.HistoryAsync()).Undone);
    }
    [Fact]
    public async Task TimerSurvivesRestartAndOnlyOneCanRun()
    {
        await OpenAsync(); var task = await Tasks.CreateAsync("Synthetic control"); var now = DateTimeOffset.UtcNow;
        await Store.StartTimerAsync(task.Id, now);
        await Assert.ThrowsAsync<SqliteException>(() => Store.StartTimerAsync(task.Id, now));
        Assert.Null(Assert.Single(await new SqliteProductivityStore(PathName).TimeEntriesAsync()).End);
        await Store.StopTimerAsync(now.AddMinutes(25));
        var entry = Assert.Single(await Store.TimeEntriesAsync());
        Assert.Equal(25, (entry.End!.Value - entry.Start).TotalMinutes);
    }
    [Fact]
    public async Task ImportIsAtomicAndRepeatImportDoesNotDuplicate()
    {
        await OpenAsync(); var now = DateTimeOffset.UtcNow;
        var task = new WorkTask("external", "Synthetic import", "comma, quote \" and\nnewline", null, "High", "Open", null, null, now, now);
        foreach (ITaskExchangeAdapter adapter in new ITaskExchangeAdapter[] { new JsonTaskExchange(), new CsvTaskExchange() })
            Assert.Equal(task, Assert.Single(adapter.Import(adapter.Export(new[] { task }))));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.ImportAsync(new[] { task, task with { Id = "bad", Title = "" } }));
        Assert.Empty(await Tasks.ListAsync(new TaskQuery("Tasks")));
        Assert.Equal(1, await Store.ImportAsync(new[] { task })); Assert.Equal(0, await Store.ImportAsync(new[] { task }));
    }
    [Fact]
    public async Task BulkChangeRollsBackAndSettingsPersist()
    {
        await OpenAsync(); var task = await Tasks.CreateAsync("Synthetic approval");
        await Assert.ThrowsAsync<ArgumentException>(() => Store.SetPrioritiesAsync(new[] { task.Id, "missing" }, "Urgent"));
        Assert.Equal("Normal", Assert.Single(await Tasks.ListAsync(new TaskQuery("Tasks"))).Priority);
        await Store.SaveSettingAsync("filter:invoice", "invoice");
        Assert.Equal("invoice", (await new SqliteProductivityStore(PathName).SettingsAsync())["filter:invoice"]);
    }
    [Fact]
    public void PlannerOrdersDeadlinesAndReportsOverloadWithoutChangingTasks()
    {
        var now = DateTimeOffset.UtcNow; var due = new DateOnly(2026, 11, 4);
        var a = new WorkTask("a", "Tomorrow", "", due.AddDays(1), "Urgent", "Open", null, null, now, now);
        var b = a with { Id = "b", Title = "Today", DueDate = due };
        var suggestions = WorkloadPlanner.Suggest(new[] { a, b }, 30, 30);
        Assert.Equal("b", suggestions[0].TaskId); Assert.True(suggestions[0].Fits); Assert.False(suggestions[1].Fits);
        Assert.Equal(due.AddDays(1), a.DueDate);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
