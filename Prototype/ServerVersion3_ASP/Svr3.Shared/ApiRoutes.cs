using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Svr3.Shared;

/// <summary>
/// HTTP endpoints. Auth: "Authorization: Bearer {sessionId}" on every request except Login.
///
/// Uploads: PUT with the raw file as the body (application/octet-stream), original name in ?fileName=.
/// Downloads: GET; original name comes back in Content-Disposition, Range requests are supported.
/// </summary>
public static class ApiRoutes
{
    public const string DefaultBaseUrl = "http://localhost:5080";

    public const string FileNameQuery = "fileName";

    // Both roles
    public const string Login = "/api/auth/login";
    public const string Logout = "/api/auth/logout";
    public const string Me = "/api/me";
    public const string SubmissionFile = "/api/submissions/{id}/file";

    // Teacher
    public const string TeacherClasses = "/api/teacher/classes";
    public const string TeacherSubmissions = "/api/teacher/assignments/{id}/submissions";
    public const string TeacherAssignmentFile = "/api/teacher/assignments/{id}/file";

    // Student
    public const string StudentAssignments = "/api/student/assignments";
    public const string StudentAssignmentFile = "/api/student/assignments/{id}/file";
    public const string StudentSubmission = "/api/student/assignments/{id}/submission";

    public static string WithId(string route, int id)
    {
        return route.Replace("{id}", id.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>JSON settings shared by server and client: camelCase, enums as names.</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
