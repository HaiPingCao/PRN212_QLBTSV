using QLBT.Server.Models;

namespace QLBT.Server.Domain;

/// <summary>Assignment rules shared by the HTTP endpoints and the admin GUI.</summary>
public static class AssignmentRules
{
    /// <summary>closed = IsClosed || (now > Deadline && !AllowLate)</summary>
    public static bool IsClosed(Assignment assignment, DateTime now)
    {
        return assignment.IsClosed || (now > assignment.DueDate && !assignment.AllowLate);
    }
}
