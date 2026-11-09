using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using QLBT.Server.Asp.SessionAndAuthentication;
using QLBT.Server.Models;
using QLBT.Server.Files;
using QLBT.Shared;
using System.IO;

namespace QLBT.Server.Asp.Endpoints;

/// <summary>Teacher-only endpoints. Every one checks that the teacher teaches the class.</summary>
public static class TeacherEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(ApiRoutes.TeacherClasses, GetMyClasses).RequireAuthorization(Policies.Teacher);
        app.MapGet(ApiRoutes.TeacherClassStudents, GetClassStudents).RequireAuthorization(Policies.Teacher);
        app.MapGet(ApiRoutes.TeacherClassAssignments, GetClassAssignments).RequireAuthorization(Policies.Teacher);
        app.MapPost(ApiRoutes.TeacherClassAssignments, CreateAssignment).RequireAuthorization(Policies.Teacher);
        app.MapPut(ApiRoutes.TeacherAssignment, UpdateAssignment).RequireAuthorization(Policies.Teacher);
        app.MapDelete(ApiRoutes.TeacherAssignment, DeleteAssignment).RequireAuthorization(Policies.Teacher);
        app.MapGet(ApiRoutes.TeacherSubmissions, GetSubmissions).RequireAuthorization(Policies.Teacher);
        app.MapPut(ApiRoutes.TeacherAssignmentFile, UploadAssignmentFile).RequireAuthorization(Policies.Teacher);
        app.MapGet(ApiRoutes.TeacherAssignmentFile, DownloadAssignmentFile).RequireAuthorization(Policies.Teacher);
        app.MapPut(ApiRoutes.TeacherSubmissionGrade, GradeSubmission).RequireAuthorization(Policies.Teacher);
        app.MapDelete(ApiRoutes.TeacherSubmission, DeleteSubmission).RequireAuthorization(Policies.Teacher);
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

    private static async Task<IResult> GetClassStudents(int id, ClaimsPrincipal user, Prn212PQlbtContext db)
    {
        IResult? denied = await CheckOwnClass(db, id, user.GetUserId());
        if (denied != null)
        {
            return denied;
        }

        List<ClassStudentDto> students = await db.ClassStudents.AsNoTracking()
            .Where(cs => cs.ClassId == id)
            .OrderBy(cs => cs.Student.Username)
            .Select(cs => new ClassStudentDto
            {
                StudentId = cs.StudentId,
                Username = cs.Student.Username,
                FullName = cs.Student.FullName,
                Email = cs.Student.Email,
                IsSuspended = !cs.IsActive
            })
            .ToListAsync();
        return Results.Ok(students);
    }

    private static async Task<IResult> GetClassAssignments(int id, ClaimsPrincipal user, Prn212PQlbtContext db)
    {
        IResult? denied = await CheckOwnClass(db, id, user.GetUserId());
        if (denied != null)
        {
            return denied;
        }

        DateTime now = DateTime.Now; // DB times are local (GETDATE())
        List<TeacherAssignmentDto> assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.ClassId == id)
            .OrderBy(a => a.DueDate)
            .Select(a => new TeacherAssignmentDto
            {
                Id = a.Id,
                ClassId = a.ClassId,
                Title = a.Title,
                Description = a.Description,
                DueDate = a.DueDate,
                AllowLate = a.AllowLate,
                IsClosedManually = a.IsClosed,
                IsClosed = a.IsClosed || (now > a.DueDate && !a.AllowLate),
                FileName = a.FileName,
                CreatedAt = a.CreatedAt,
                SubmissionCount = a.Submissions.Count(),
                GradedCount = a.Submissions.Count(s => s.Score != null)
            })
            .ToListAsync();
        return Results.Ok(assignments);
    }

    /// <summary>CreateAssignment without the file: the client uploads it next with PUT TeacherAssignmentFile.</summary>
    private static async Task<IResult> CreateAssignment(int id, AssignmentEditRequest request, ClaimsPrincipal user, Prn212PQlbtContext db)
    {
        IResult? denied = await CheckOwnClass(db, id, user.GetUserId());
        if (denied != null)
        {
            return denied;
        }
        string? invalid = Validate(request);
        if (invalid != null)
        {
            return ApiResults.Fail(ApiStatus.Rejected, invalid);
        }

        Assignment assignment = new Assignment
        {
            ClassId = id,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DueDate = request.DueDate,
            AllowLate = request.AllowLate,
            IsClosed = false,
            CreatedAt = DateTime.Now
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();
        return Results.Ok(new CreatedResult { Id = assignment.Id });
    }

    /// <summary>Rename, extend, toggle late submission, close or reopen. Existing scores are kept.</summary>
    private static async Task<IResult> UpdateAssignment(int id, AssignmentEditRequest request, ClaimsPrincipal user, Prn212PQlbtContext db)
    {
        Assignment? assignment = await db.Assignments.Include(a => a.Class).FirstOrDefaultAsync(a => a.Id == id);
        IResult? denied = CheckOwnAssignment(assignment, user.GetUserId());
        if (denied != null)
        {
            return denied;
        }
        string? invalid = Validate(request);
        if (invalid != null)
        {
            return ApiResults.Fail(ApiStatus.Rejected, invalid);
        }

        assignment!.Title = request.Title.Trim();
        assignment.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        assignment.DueDate = request.DueDate;
        assignment.AllowLate = request.AllowLate;
        assignment.IsClosed = request.IsClosed;
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    /// <summary>Deletes the assignment and its submissions in the DB first (cascade), then the whole folder.</summary>
    private static async Task<IResult> DeleteAssignment(int id, ClaimsPrincipal user, Prn212PQlbtContext db, FileStorage storage)
    {
        Assignment? assignment = await db.Assignments.AsNoTracking().Include(a => a.Class).FirstOrDefaultAsync(a => a.Id == id);
        IResult? denied = CheckOwnAssignment(assignment, user.GetUserId());
        if (denied != null)
        {
            return denied;
        }

        using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Submissions.Where(s => s.AssignmentId == id).ExecuteDeleteAsync();
            await db.Assignments.Where(a => a.Id == id).ExecuteDeleteAsync();
            await transaction.CommitAsync();
        }

        DeleteDirectoryQuietly(storage.AssignmentDirectory(assignment!.Class.SemesterId, assignment.ClassId, assignment.Id));
        return Results.Ok();
    }

    private static async Task<IResult> GetSubmissions(int id, ClaimsPrincipal user, Prn212PQlbtContext db)
    {
        Assignment? assignment = await db.Assignments.AsNoTracking().Include(a => a.Class).FirstOrDefaultAsync(a => a.Id == id);
        IResult? denied = CheckOwnAssignment(assignment, user.GetUserId());
        if (denied != null)
        {
            return denied;
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
                FileName = s.FileName,
                Comment = s.Comment,
                SubmitCount = s.SubmitCount
            })
            .ToListAsync();
        return Results.Ok(submissions);
    }

    /// <summary>Replaces the assignment file (the file part of Create/UpdateAssignment). Body = raw file bytes.</summary>
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
        IResult? denied = CheckOwnAssignment(assignment, user.GetUserId());
        if (denied != null)
        {
            return denied;
        }

        string target = storage.AssignmentFile(assignment!.Class.SemesterId, assignment.ClassId, assignment.Id);
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

    private static async Task<IResult> DownloadAssignmentFile(int id, ClaimsPrincipal user, Prn212PQlbtContext db, FileStorage storage)
    {
        Assignment? assignment = await db.Assignments.AsNoTracking().Include(a => a.Class).FirstOrDefaultAsync(a => a.Id == id);
        IResult? denied = CheckOwnAssignment(assignment, user.GetUserId());
        if (denied != null)
        {
            return denied;
        }
        if (assignment!.FileName == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "This assignment has no file.");
        }
        return ApiResults.File(storage.AssignmentFile(assignment.Class.SemesterId, assignment.ClassId, assignment.Id), assignment.FileName);
    }

    /// <summary>Grading is only allowed while the assignment is closed. Re-grading overwrites score and comment.</summary>
    private static async Task<IResult> GradeSubmission(int id, GradeRequest request, ClaimsPrincipal user, Prn212PQlbtContext db)
    {
        Submission? submission = await db.Submissions
            .Include(s => s.Assignment).ThenInclude(a => a.Class)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (submission == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "Submission not found.");
        }
        if (submission.Assignment.Class.TeacherId != user.GetUserId())
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "You do not teach this class.");
        }
        if (!AssignmentRules.IsClosed(submission.Assignment, DateTime.Now))
        {
            return ApiResults.Fail(ApiStatus.Rejected, "Close the assignment before grading.");
        }
        if (request.Score < 0 || request.Score > 10 || decimal.Round(request.Score, 2) != request.Score)
        {
            return ApiResults.Fail(ApiStatus.Rejected, "Score must be between 0 and 10 with at most 2 decimals.");
        }

        submission.Score = request.Score;
        submission.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    /// <summary>Removes the submission and its score (DB first, then the file) so the student can submit again.</summary>
    private static async Task<IResult> DeleteSubmission(int id, ClaimsPrincipal user, Prn212PQlbtContext db, FileStorage storage)
    {
        Submission? submission = await db.Submissions
            .Include(s => s.Assignment).ThenInclude(a => a.Class)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (submission == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "Submission not found.");
        }
        if (submission.Assignment.Class.TeacherId != user.GetUserId())
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "You do not teach this class.");
        }

        string path = storage.SubmissionFile(submission.Assignment.Class.SemesterId, submission.ClassId, submission.AssignmentId, submission.Id);
        db.Submissions.Remove(submission);
        await db.SaveChangesAsync();
        FileReceiver.DeleteQuietly(path);
        return Results.Ok();
    }

    /// <returns>null when the class exists and the teacher teaches it, otherwise the failure result.</returns>
    private static async Task<IResult?> CheckOwnClass(Prn212PQlbtContext db, int classId, int teacherId)
    {
        int? owner = await db.Classes.AsNoTracking().Where(c => c.Id == classId).Select(c => (int?)c.TeacherId).FirstOrDefaultAsync();
        if (owner == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "Class not found.");
        }
        if (owner != teacherId)
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "You do not teach this class.");
        }
        return null;
    }

    /// <summary>The assignment must be loaded with its Class.</summary>
    private static IResult? CheckOwnAssignment(Assignment? assignment, int teacherId)
    {
        if (assignment == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "Assignment not found.");
        }
        if (assignment.Class.TeacherId != teacherId)
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "You do not teach this class.");
        }
        return null;
    }

    private static string? Validate(AssignmentEditRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Title is required.";
        }
        if (request.Title.Trim().Length > 255)
        {
            return "Title is longer than 255 characters.";
        }
        if (request.DueDate == default)
        {
            return "Due date is required.";
        }
        return null;
    }

    private static void DeleteDirectoryQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
