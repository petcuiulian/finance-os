namespace FinanceOS.Core;

public sealed record NotificationOptions(bool Toasts, bool Sound, bool WorkingHoursOnly, bool WeekdaysOnly);

public sealed record ReminderDraft(string TaskId, DateTime LocalTime, string TimeZoneId,
    string Recurrence = "Once", int IntervalMinutes = 60);
public sealed record ReminderAlert(string Id, string TaskId, string Title, DateTimeOffset ScheduledUtc, DateTimeOffset AvailableUtc);
public sealed record ReminderScheduleEntry(string Id, string Title, DateTimeOffset DueUtc, string Recurrence, string TimeZoneId);
public sealed record CalendarEntry(string Id, string Title, string Kind, DateTime Start, DateTime? End);

public static class ReminderSchedule
{
    public static DateTimeOffset ToUtc(DateTime local, string timeZoneId, bool shiftGap = false)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            if (!shiftGap) throw new ArgumentException("This time does not exist because the clock changes. Choose another time.");
            do { local = local.AddMinutes(1); } while (zone.IsInvalidTime(local));
        }
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Min() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static DateTimeOffset? NextAfter(ReminderDraft draft, DateTimeOffset after)
    {
        if (draft.Recurrence == "Once") return null;
        var first = ToUtc(draft.LocalTime, draft.TimeZoneId);
        if (draft.Recurrence == "Interval")
        {
            if (draft.IntervalMinutes is < 1 or > 525600) throw new ArgumentException("Interval must be 1–525600 minutes.");
            var count = Math.Max(1, Math.Floor((after - first).TotalMinutes / draft.IntervalMinutes) + 1);
            return first.AddMinutes(count * draft.IntervalMinutes);
        }
        var localAfter = TimeZoneInfo.ConvertTime(after, TimeZoneInfo.FindSystemTimeZoneById(draft.TimeZoneId)).DateTime;
        var anchor = draft.LocalTime;
        var n = draft.Recurrence switch
        {
            "Daily" => Math.Max(1, (localAfter.Date - anchor.Date).Days),
            "Weekly" => Math.Max(1, (localAfter.Date - anchor.Date).Days / 7),
            "Monthly" => Math.Max(1, (localAfter.Year - anchor.Year) * 12 + localAfter.Month - anchor.Month),
            _ => throw new ArgumentException("Unknown recurrence.")
        };
        while (true)
        {
            var candidate = draft.Recurrence switch
            {
                "Daily" => anchor.AddDays(n),
                "Weekly" => anchor.AddDays(n * 7),
                _ => anchor.AddMonths(n)
            };
            var utc = ToUtc(candidate, draft.TimeZoneId, shiftGap: true);
            if (utc > after) return utc;
            n++;
        }
    }
}
