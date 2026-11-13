using Microsoft.AspNetCore.Http;
using QLBT.Shared;

namespace QLBT.Server.Asp.Endpoints;

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
            return Fail(ApiStatus.NotFound, "The file could not be found on the server.");
        }
        return Results.File(path, "application/octet-stream", fileName, enableRangeProcessing: true);
    }
}


