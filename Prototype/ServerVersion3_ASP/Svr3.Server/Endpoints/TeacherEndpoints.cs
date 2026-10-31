using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Svr3.Server.Auth;
using Svr3.Server.Data;
using Svr3.Server.Files;
using Svr3.Shared;

namespace Svr3.Server.Endpoints;

/// <summary>Teacher-only endpoints. Every one checks that the teacher teaches the class.</summary>
public static class TeacherEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(ApiRoutes.TeacherClasses, GetMyClasses).RequireAuthorization(Policies.Teacher);
        app.MapGet(ApiRoutes.TeacherSubmissions, GetSubmissions).RequireAuthorization(Policies.Teacher);
        app.MapPut(ApiRoutes.TeacherAssignmentFile, UploadAssignmentFile).RequireAuthorization(Policies.Teacher);
    }

    private static async Task<IResult> GetMyClasses(ClaimsPrincipal user, Prn212PQlbtContext db)
    {
        int teacherId = user.GetUserId();
        List<ClassDto> classes = await db.Classes.AsNoTracking()
            .Where(c => c.TeacherId == teacherId)
            .OrderBy(c => c.SemesterId).ThenBy(c => c.Name)
            .Select(c => new ClassDto
            {
                Id = c.Id,
                Name = c.Name,
                Major = c.Major,
                Semester = c.Semester.Name,
                StudentCount = c.ClassStudents.Count(cs => cs.IsActive)
            })
            .ToListAsync();
        return Results.Ok(classes);
    }

    private static async Task<IResult> GetSubmissions(int id, ClaimsPrincipal user, Prn212PQlbtContext db)
    {
        Assignment? assignment = await db.Assignments.AsNoTracking().Include(a => a.Class).FirstOrDefaultAsync(a => a.Id == id);
        if (assignment == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "Assignment not found.");
        }
        if (assignment.Class.TeacherId != user.GetUserId())
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "You do not teach this class.");
        }

        List<SubmissionDto> submissions = await db.Submissions.AsNoTracking()
            .Where(s => s.AssignmentId == id)
            .OrderBy(s => s.ClassStudent.Student.Username)
            .Select(s => new SubmissionDto
            {
                Id = s.Id,
                StudentId = s.StudentId,
                StudentUsername = s.ClassStudent.Student.Username,
                StudentName = s.ClassStudent.Student.FullName,
                SubmittedAt = s.SubmittedAt,
                IsLate = s.IsLate,
                Score = s.Score,
                FileName = s.FileName
            })
            .ToListAsync();
        return Results.Ok(submissions);
    }

    /// <summary>Replaces the assignment file (the file part of UpdateAssignment). Body = raw file bytes.</summary>
    private static async Task<IResult> UploadAssignmentFile(
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

        // All checks happen before the body is read: with "Expect: 100-continue" the client never sends the bytes.
        Assignment? assignment = await db.Assignments.Include(a => a.Class).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assignment == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "Assignment not found.");
        }
        if (assignment.Class.TeacherId != user.GetUserId())
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "You do not teach this class.");
        }

        string target = storage.AssignmentFile(assignment.Class.SemesterId, assignment.ClassId, assignment.Id);
        (string tempPath, long length) = await FileReceiver.ReceiveToTemp(request.Body, Path.GetDirectoryName(target)!, ct);
        try
        {
            using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct))
            {
                assignment.FileName = cleanName;
                assignment.FilePath = storage.ToRelative(target);
                await db.SaveChangesAsync(ct);
                File.Move(tempPath, target, overwrite: true);
                await transaction.CommitAsync(ct);
            }
        }
        finally
        {
            FileReceiver.DeleteQuietly(tempPath);
        }

        return Results.Ok(new UploadResult { FileName = cleanName, Length = length });
    }
}
