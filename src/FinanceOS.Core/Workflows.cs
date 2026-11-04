namespace FinanceOS.Core;

public sealed record WorkflowTemplate(string Id, string Name);
public sealed record WorkflowStage(string Id, string TemplateId, string Name, int Position);
public sealed record WorkflowCard(string TaskId, string Title, string StageId, string StageName, string Status);
public sealed record WaitingTask(string TaskId, string Title, string Contact, DateOnly? ExpectedDate,
    DateOnly FollowupDate, DateTimeOffset StartedUtc, bool Active);

public static class BusinessDays
{
    public static int Count(DateOnly start, DateOnly end, ISet<DateOnly>? holidays = null)
    {
        var count = 0;
        for (var day = start.AddDays(1); day <= end; day = day.AddDays(1))
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && holidays?.Contains(day) != true) count++;
        return count;
    }

    public static DateOnly Add(DateOnly date, int days, ISet<DateOnly>? holidays = null)
    {
        if (days < 0 || days > 3660) throw new ArgumentOutOfRangeException(nameof(days));
        while (days > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidays?.Contains(date) == true) continue;
            days--;
        }
        return date;
    }
}
