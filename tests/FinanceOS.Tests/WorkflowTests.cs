using FinanceOS.Core;
using FinanceOS.Infrastructure;
using Xunit;

namespace FinanceOS.Tests;

public sealed class WorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "finance-workflow-tests", Guid.NewGuid().ToString("N"));
    private string Database => AppDataPaths.DatabasePath(_root);
    private SqliteWorkflowStore Workflows => new(Database);
    private SqliteTaskStore Tasks => new(Database);
    private async Task OpenAsync() => await new SqliteAppStore(Database).OpenSessionAsync();

    [Fact]
    public async Task TemplatesStagesKanbanChecklistAndStatusPersist()
    {
        await OpenAsync();
        Assert.Equal(5, (await Workflows.TemplatesAsync()).Count);
        var template = await Workflows.CreateTemplateAsync("Quarter close");
        var first = await Workflows.AddStageAsync(template, "Request approval");
        var second = await Workflows.AddStageAsync(template, "Post journal");
        await Workflows.MoveStageAsync(second, -1);
        Assert.Equal(second, (await Workflows.StagesAsync(template))[0].Id);
        await Workflows.RenameStageAsync(first, "Approved");
        var task = await Tasks.CreateAsync("Synthetic journal");
        await Workflows.AssignTaskAsync(task.Id, template, first);
        Assert.Equal("Approved", Assert.Single(await Workflows.CardsAsync(template)).StageName);
        await Workflows.AddChecklistAsync(task.Id, first, "Sign-off received");
        var item = Assert.Single(await Workflows.ChecklistAsync(task.Id, first));
        await Workflows.SetChecklistAsync(item.Id, true);
        Assert.True(Assert.Single(await Workflows.ChecklistAsync(task.Id, first)).Done);
        await Workflows.AddStatusAsync("Needs review");
        await Workflows.SetCustomStatusAsync(task.Id, "Needs review");
        Assert.Equal("Needs review", Assert.Single(await new SqliteWorkflowStore(Database).CardsAsync(template)).Status);
    }

    [Fact]
    public async Task WaitingPersistsHistoryAndCancelsFollowupOnCompletion()
    {
        await OpenAsync();
        var task = await Tasks.CreateAsync("Synthetic vendor response");
        await Workflows.StartWaitingAsync(task.Id, "Vendor", new DateOnly(2026, 11, 6),
            new DateOnly(2026, 11, 9), "UTC", "Requested invoice copy");
        Assert.Equal("Vendor", Assert.Single(await new SqliteWorkflowStore(Database).WaitingAsync()).Contact);
        Assert.Single(await Workflows.WaitingHistoryAsync(task.Id));
        await Tasks.SetStatusAsync(task.Id, "Done");
        Assert.Empty(await Workflows.WaitingAsync());
        await new SqliteReminderStore(Database).ReconcileAsync(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        Assert.Empty(await new SqliteReminderStore(Database).AlertsAsync(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 11, 9), BusinessDays.Add(new DateOnly(2026, 11, 5), 2));
        Assert.Equal(2, BusinessDays.Count(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 9)));
    }

    [Fact]
    public async Task DependenciesBlockPrematureCompletionAndRejectCycles()
    {
        await OpenAsync();
        var request = await Tasks.CreateAsync("Synthetic request");
        var approval = await Tasks.CreateAsync("Synthetic approval");
        await Workflows.AddDependencyAsync(request.Id, approval.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => Workflows.AddDependencyAsync(approval.Id, request.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Tasks.SetStatusAsync(request.Id, "Done"));
        await Tasks.SetStatusAsync(approval.Id, "Done");
        await Tasks.SetStatusAsync(request.Id, "Done");
        Assert.Equal(2, (await Tasks.ListAsync(new TaskQuery("Tasks", Filter: "Done"))).Count);
    }

    [Fact]
    public async Task StageDeadlineCreatesReminderAndRecurringMonthEndIsIdempotent()
    {
        await OpenAsync();
        var task = await Tasks.CreateAsync("Synthetic monthly control");
        var template = await Workflows.CreateTemplateAsync("Control");
        var stage = await Workflows.AddStageAsync(template, "Review");
        await Workflows.AddStageDeadlineAsync(stage, 3, 60);
        await Workflows.AssignTaskAsync(task.Id, template, stage);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).AddDays(3), await Workflows.CardDeadlineAsync(task.Id));
        Assert.Equal((3, 60), await Workflows.StageDeadlineAsync(stage));
        await Workflows.AddRecurringAsync(task.Id, "Monthly", new DateOnly(2026, 1, 31));
        Assert.Equal(3, await Workflows.ReconcileRecurringAsync(new DateOnly(2026, 3, 31)));
        Assert.Equal(0, await Workflows.ReconcileRecurringAsync(new DateOnly(2026, 3, 31)));
        var instances = await Tasks.ListAsync(new TaskQuery("Tasks"));
        Assert.Equal(4, instances.Count);
        Assert.Contains(instances, x => x.DueDate == new DateOnly(2026, 2, 28));
    }

    [Fact]
    public async Task AccountantCanTrackManyRequestsApprovalsAndClosingItems()
    {
        await OpenAsync();
        var templates = await Workflows.TemplatesAsync();
        var ap = Assert.Single(templates, x => x.Id == "ap");
        var closing = Assert.Single(templates, x => x.Id == "month-end");
        var apStages = await Workflows.StagesAsync(ap.Id);
        var closingStages = await Workflows.StagesAsync(closing.Id);
        var tasks = await Task.WhenAll(Enumerable.Range(0, 100).Select(i => Tasks.CreateAsync($"Synthetic invoice {i:000}")));
        foreach (var task in tasks.Take(60)) await Workflows.AssignTaskAsync(task.Id, ap.Id, apStages[0].Id);
        foreach (var task in tasks.Skip(60)) await Workflows.AssignTaskAsync(task.Id, closing.Id, closingStages[0].Id);
        foreach (var task in tasks.Take(20))
            await Workflows.StartWaitingAsync(task.Id, "Synthetic approver", null, new DateOnly(2026, 11, 9), "UTC", "Approval requested");
        foreach (var task in tasks.Skip(20).Take(10))
            await Workflows.AddDependencyAsync(task.Id, tasks[0].Id);
        Assert.Equal(60, (await Workflows.CardsAsync(ap.Id)).Count);
        Assert.Equal(40, (await Workflows.CardsAsync(closing.Id)).Count);
        Assert.Equal(20, (await Workflows.WaitingAsync()).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Tasks.SetStatusAsync(tasks[20].Id, "Done"));
        await Tasks.SetStatusAsync(tasks[0].Id, "Done");
        await Tasks.SetStatusAsync(tasks[20].Id, "Done");
        Assert.Equal(19, (await Workflows.WaitingAsync()).Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
