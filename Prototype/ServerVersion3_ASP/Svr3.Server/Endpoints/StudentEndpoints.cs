using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Svr3.Server.Auth;
using Svr3.Server.Data;
using Svr3.Server.Files;
using Svr3.Shared;

namespace Svr3.Server.Endpoints;

/// <summary>Student-only endpoints. A student only reaches classes they are enrolled in.</summary>
public static class StudentEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(ApiRoutes.StudentAssignments, GetMyAssignments).RequireAuthorization(Policies.Student);
        app.MapGet(ApiRoutes.StudentAssignmentFile, DownloadAssignment).RequireAuthorization(Policies.Student);
        app.MapPut(ApiRoutes.StudentSubmission, SubmitAssignment).RequireAuthorization(Policies.Student);
    }

    private static async Task<IResult> GetMyAssignments(ClaimsPrincipal user, Prn212PQlbtContext db)
    {
        int studentId = user.GetUserId();
        DateTime now = DateTime.Now; // DB times are local (GETDATE())
        List<StudentAssignmentDto> assignments = await db.ClassStudents.AsNoTracking()
            .Where(cs => cs.StudentId == studentId)
            .SelectMany(cs => cs.Class.Assignments.Select(a => new StudentAssignmentDto
            {
                Id = a.Id,
                ClassId = a.ClassId,
                ClassName = cs.Class.Name,
                Title = a.Title,
                DueDate = a.DueDate,
                IsClosed = a.IsClosed || (now > a.DueDate && !a.AllowLate),
                HasFile = a.FileName != null,
                ClassSuspended = !cs.IsActive
            }))
            .OrderBy(a => a.DueDate)
            .ToListAsync();
        return Results.Ok(assignments);
    }

    private static async Task<IResult> DownloadAssignment(int id, ClaimsPrincipal user, Prn212PQlbtContext db, FileStorage storage)
    {
        Assignment? assignment = await db.Assignments.AsNoTracking().Include(a => a.Class).FirstOrDefaultAsync(a => a.Id == id);
        if (assignment == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "Assignment not found.");
        }

        IResult? denied = await CheckActiveMember(db, assignment.ClassId, user.GetUserId());
        if (denied != null)
        {
            return denied;
        }
        if (assignment.FileName == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "This assignment has no file.");
        }

        string path = storage.AssignmentFile(assignment.Class.SemesterId, assignment.ClassId, assignment.Id);
        return ApiResults.File(path, assignment.FileName);
    }

    /// <summary>
    /// Checks in Plan.md order: member and not suspended, not closed, not graded; then IsLate = now > Deadline.
    /// The body is only read after the checks pass, and the old file is kept until the new one is complete.
    /// </summary>
    private static async Task<IResult> SubmitAssignment(
        int id,
        string? fileName,
        HttpRequest request,
        ClaimsPrincipal user,
        Prn212PQlbtContext db,
        FileStorage storage,
        CancellationToken ct)
    {
        string? cleanName = FileReceiver.CleanFileName(fileName);
        if (cleanName == null)
        {
            return ApiResults.Fail(ApiStatus.Rejected, "Missing or invalid file name.");
        }

        int studentId = user.GetUserId();
        Assignment? assignment = await db.Assignments.AsNoTracking().Include(a => a.Class).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assignment == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "Assignment not found.");
        }

        IResult? denied = await CheckActiveMember(db, assignment.ClassId, studentId);
        if (denied != null)
        {
            return denied;
        }

        DateTime now = DateTime.Now;
        if (AssignmentRules.IsClosed(assignment, now))
        {
            return ApiResults.Fail(ApiStatus.Rejected, "Assignment is closed.");
        }

        Submission? submission = await db.Submissions.FirstOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == studentId, ct);
        if (submission != null && submission.Score != null)
        {
            return ApiResults.Fail(ApiStatus.Rejected, "Submission is already graded.");
        }

        int semesterId = assignment.Class.SemesterId;
        string directory = storage.SubmissionsDirectory(semesterId, assignment.ClassId, assignment.Id);
        (string tempPath, long length) = await FileReceiver.ReceiveToTemp(request.Body, directory, ct);
        try
        {
            using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct))
            {
                if (submission == null)
                {
                    submission = new Submission
                    {
                        AssignmentId = assignment.Id,
                        ClassId = assignment.ClassId,
                        StudentId = studentId,
                        FilePath = "",
                        SubmitCount = 1
                    };
                    db.Submissions.Add(submission);
                }
                else
                {
                    submission.SubmitCount++;
                }

                submission.FileName = cleanName;
                submission.SubmittedAt = now;
                submission.IsLate = now > assignment.DueDate;
                await db.SaveChangesAsync(ct); // assigns the id of a new submission

                string target = storage.SubmissionFile(semesterId, assignment.ClassId, assignment.Id, submission.Id);
                submission.FilePath = storage.ToRelative(target);
                await db.SaveChangesAsync(ct);

                File.Move(tempPath, target, overwrite: true);
                await transaction.CommitAsync(ct);
            }
        }
        finally
        {
            FileReceiver.DeleteQuietly(tempPath);
        }

        return Results.Ok(new SubmitResult
        {
            SubmissionId = submission.Id,
            IsLate = submission.IsLate,
            SubmitCount = submission.SubmitCount,
            Length = length
        });
    }

    /// <returns>null when the student is an active member of the class, otherwise the Forbidden result.</returns>
    private static async Task<IResult?> CheckActiveMember(Prn212PQlbtContext db, int classId, int studentId)
    {
        ClassStudent? member = await db.ClassStudents.AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentId);
        if (member == null)
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "You are not in this class.");
        }
        if (!member.IsActive)
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "You are suspended in this class.");
        }
        return null;
    }
}
