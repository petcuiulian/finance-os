namespace FinanceOS.Core;

public sealed record WorkTask(
    string Id, string Title, string Notes, DateOnly? DueDate, string Priority, string Status,
    string? ProjectId, string? CategoryId, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc);

public sealed record TaskQuery(string View, string Search = "", string Filter = "All", DateOnly? Today = null);

public interface ITaskStore
{
    Task<WorkTask> CreateAsync(string title, string notes = "", DateOnly? dueDate = null,
        string priority = "Normal", string? projectId = null, string? categoryId = null,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkTask task, CancellationToken cancellationToken = default);
    Task SetStatusAsync(string id, string status, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkTask>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default);
}
