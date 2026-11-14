using QLBT.Server.Models;

namespace QLBT.Server.Domain;

/// <summary>Assignment rules shared by the HTTP endpoints and the admin GUI.</summary>
public static class AssignmentRules
{
    /// <summary>closed = IsClosed || (now > Deadline && !AllowLate)</summary>
    public static bool IsClosed(Assignment assignment, DateTime now)
    {
        return IsClosed(assignment.IsClosed, assignment.DueDate, assignment.AllowLate, now);
    }

    /// <summary>Same rule from the raw columns (used by the admin grid rows).</summary>
    public static bool IsClosed(bool closedManually, DateTime dueDate, bool allowLate, DateTime now)
    {
        return closedManually || (now > dueDate && !allowLate);
    }
}
