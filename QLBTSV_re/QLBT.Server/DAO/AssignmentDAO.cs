using Microsoft.EntityFrameworkCore;
using QLBT.Server.Admin;
using QLBT.Server.Models;

namespace QLBT.Server.DAO;

/// <summary>Read-only queries on assignments and submissions for the admin GUI. One short-lived context per call.</summary>
public sealed class AssignmentDAO
{
    /// <summary>The assignments of a class, newest due date first, with submission and graded counts.</summary>
    public async Task<List<AssignmentRow>> ListByClass(int classId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Assignments.AsNoTracking()
                .Where(a => a.ClassId == classId)
                .OrderByDescending(a => a.DueDate)
                .Select(a => new AssignmentRow
                {
                    Id = a.Id,
                    Title = a.Title,
                    DueDate = a.DueDate,
                    AllowLate = a.AllowLate,
                    ClosedManually = a.IsClosed,
                    FileName = a.FileName,
                    SubmissionCount = a.Submissions.Count,
                    GradedCount = a.Submissions.Count(s => s.Score != null)
                })
                .ToListAsync();
        }
    }

    /// <summary>The submissions of an assignment, by student username.</summary>
    public async Task<List<SubmissionRow>> ListSubmissions(int assignmentId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Submissions.AsNoTracking()
                .Where(s => s.AssignmentId == assignmentId)
                .OrderBy(s => s.ClassStudent.Student.Username)
                .Select(s => new SubmissionRow
                {
                    Id = s.Id,
                    AssignmentId = s.AssignmentId,
                    Username = s.ClassStudent.Student.Username,
                    FullName = s.ClassStudent.Student.FullName,
                    SubmittedAt = s.SubmittedAt,
                    SubmitCount = s.SubmitCount,
                    IsLate = s.IsLate,
                    Score = s.Score,
                    Comment = s.Comment,
                    FileName = s.FileName
                })
                .ToListAsync();
        }
    }
}
