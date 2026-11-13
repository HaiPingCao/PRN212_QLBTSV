namespace QLBT.Server.Domain;

public enum SemesterStatus
{
    NotStarted,
    Running,
    Ended
}

/// <summary>Semester status for display (Semesters tab, default semester filter of the Classes tab).</summary>
public static class SemesterRules
{
    public static SemesterStatus Status(DateOnly startDate, DateOnly endDate)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        if (today < startDate)
        {
            return SemesterStatus.NotStarted;
        }
        return today > endDate ? SemesterStatus.Ended : SemesterStatus.Running;
    }
}
