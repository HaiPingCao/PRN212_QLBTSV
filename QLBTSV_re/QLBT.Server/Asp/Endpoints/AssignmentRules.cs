using QLBT.Server.Models;

namespace QLBT.Server.Asp.Endpoints
{
    /// <summary>Streams a stored file; the original name goes into Content-Disposition (UTF-8 safe).</summary>
    public static class AssignmentRules
    {
        /// <summary>closed = IsClosed || (now > Deadline && !AllowLate)</summary>
        public static bool IsClosed(Assignment assignment, DateTime now)
        {
            return assignment.IsClosed || (now > assignment.DueDate && !assignment.AllowLate);
        }
    }
}
