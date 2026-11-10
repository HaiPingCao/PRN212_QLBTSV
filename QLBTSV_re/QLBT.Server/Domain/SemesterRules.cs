using QLBT.Server.Models;

namespace QLBT.Server.Domain;

public enum SemesterStatus
{
    NotStarted,
    Running,
    Ended
}

/// <summary>Plan.md: semesters and their classes can be edited or deleted only before the semester starts.</summary>
public static class SemesterRules
{
    public static DateOnly Today()
    {
        return DateOnly.FromDateTime(DateTime.Today);
    }

    public static bool HasStarted(DateOnly startDate, DateOnly today)
    {
        return today >= startDate;
    }

    public static bool HasStarted(Semester semester)
    {
        return HasStarted(semester.StartDate, Today());
    }

    public static SemesterStatus Status(DateOnly startDate, DateOnly endDate, DateOnly today)
    {
        if (today < startDate)
        {
            return SemesterStatus.NotStarted;
        }
        return today > endDate ? SemesterStatus.Ended : SemesterStatus.Running;
    }

    /// <summary>After the start, the end date can only move later.</summary>
    public static bool CanSetEndDate(Semester current, DateOnly newEnd)
    {
        return !HasStarted(current) || newEnd >= current.EndDate;
    }
}
