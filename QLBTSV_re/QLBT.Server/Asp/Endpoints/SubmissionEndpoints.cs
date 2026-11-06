using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QLBT.Server.Asp.SessionAndAuthentication;
using QLBT.Server.Files;
using QLBT.Server.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using QLBT.Shared;

namespace QLBT.Server.Asp.Endpoints;

/// <summary>Endpoints for both roles.</summary>
public static class SubmissionEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(ApiRoutes.SubmissionFile, DownloadSubmission).RequireAuthorization();
    }

    /// <summary>Teacher of the class, or the student who owns it (suspended students still can).</summary>
    private static async Task<IResult> DownloadSubmission(int id, ClaimsPrincipal user, Prn212PQlbtContext db, FileStorage storage)
    {
        Submission? submission = await db.Submissions.AsNoTracking()
            .Include(s => s.Assignment).ThenInclude(a => a.Class)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (submission == null)
        {
            return ApiResults.Fail(ApiStatus.NotFound, "Submission not found.");
        }

        int userId = user.GetUserId();
        bool allowed;
        if (user.GetRole() == UserRole.Teacher)
        {
            allowed = submission.Assignment.Class.TeacherId == userId;
        }
        else
        {
            allowed = submission.StudentId == userId;
        }
        if (!allowed)
        {
            return ApiResults.Fail(ApiStatus.Forbidden, "This submission is not yours.");
        }

        string path = storage.SubmissionFile(submission.Assignment.Class.SemesterId, submission.ClassId, submission.AssignmentId, submission.Id);
        return ApiResults.File(path, submission.FileName);
    }
}
