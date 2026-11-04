namespace FinanceOS.Core;

public sealed record AutomationRule(string Id, string Name, string Trigger, string ContainsText, string Priority);
public sealed record AutomationChange(string Id, string TaskId, string Before, string After, bool Undone);
public sealed record TimeEntry(string Id, string TaskId, DateTimeOffset Start, DateTimeOffset? End);
public sealed record PlanSuggestion(string TaskId, string Title, int Minutes, bool Fits, string Reason);
public interface ITaskExchangeAdapter
{
    string Format { get; }
    string Export(IReadOnlyList<WorkTask> tasks);
    IReadOnlyList<WorkTask> Import(string content);
}

public static class WorkloadPlanner
{
    public static IReadOnlyList<PlanSuggestion> Suggest(IEnumerable<WorkTask> tasks, int availableMinutes, int estimatedMinutes)
    {
        if (availableMinutes < 0 || estimatedMinutes < 1) throw new ArgumentException("Invalid planning capacity.");
        return tasks.Where(t => t.Status == "Open").OrderBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ThenBy(t => t.Priority switch { "Urgent" => 0, "High" => 1, "Normal" => 2, _ => 3 })
            .Select(t =>
            {
                var fits = availableMinutes >= estimatedMinutes; if (fits) availableMinutes -= estimatedMinutes;
                return new PlanSuggestion(t.Id, t.Title, estimatedMinutes, fits, fits ? "Suggested within remaining capacity" : "Capacity exceeded — review workload");
            }).ToList();
    }
}
