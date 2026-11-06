using Svr3.Server.Data;
using Svr3.Shared;

namespace Svr3.Server.Endpoints;

public static class ApiResults
{
    public static IResult Fail(ApiStatus status, string error)
    {
        return Results.Json(new ApiError { Status = status, Error = error }, ApiJson.Options, statusCode: ToHttpStatus(status));
    }

    public static int ToHttpStatus(ApiStatus status)
    {
        switch (status)
        {
            case ApiStatus.Ok:
                return StatusCodes.Status200OK;
            case ApiStatus.InvalidCredentials:
            case ApiStatus.Unauthorized:
                return StatusCodes.Status401Unauthorized;
            case ApiStatus.Forbidden:
                return StatusCodes.Status403Forbidden;
            case ApiStatus.NotFound:
                return StatusCodes.Status404NotFound;
            case ApiStatus.Rejected:
                return StatusCodes.Status409Conflict;
            default:
                return StatusCodes.Status500InternalServerError;
        }
    }

    /// <summary>Streams a stored file; the original name goes into Content-Disposition (UTF-8 safe).</summary>
    public static IResult File(string path, string fileName)
    {
        if (!System.IO.File.Exists(path))
        {
            return Fail(ApiStatus.NotFound, "File is missing on the server.");
        }
        return Results.File(path, "application/octet-stream", fileName, enableRangeProcessing: true);
    }
}

public static class AssignmentRules
{
    /// <summary>closed = IsClosed || (now > Deadline && !AllowLate)</summary>
    public static bool IsClosed(Assignment assignment, DateTime now)
    {
        return assignment.IsClosed || (now > assignment.DueDate && !assignment.AllowLate);
    }
}
