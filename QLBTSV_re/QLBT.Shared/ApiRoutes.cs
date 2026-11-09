using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QLBT.Shared;

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
    /// <summary>GET: students of class {id}.</summary>
    public const string TeacherClassStudents = "/api/teacher/classes/{id}/students";
    /// <summary>GET: assignments of class {id}. POST: create one (JSON only; then upload the file with TeacherAssignmentFile).</summary>
    public const string TeacherClassAssignments = "/api/teacher/classes/{id}/assignments";
    /// <summary>PUT: update title / description / due date / allowLate / isClosed. DELETE: delete with all submissions and files.</summary>
    public const string TeacherAssignment = "/api/teacher/assignments/{id}";
    public const string TeacherSubmissions = "/api/teacher/assignments/{id}/submissions";
    /// <summary>PUT: upload or replace the assignment file. GET: download it.</summary>
    public const string TeacherAssignmentFile = "/api/teacher/assignments/{id}/file";
    /// <summary>DELETE: remove submission {id} and its score so the student can submit again.</summary>
    public const string TeacherSubmission = "/api/teacher/submissions/{id}";
    /// <summary>PUT: grade or re-grade submission {id}. Only while the assignment is closed.</summary>
    public const string TeacherSubmissionGrade = "/api/teacher/submissions/{id}/grade";

    // Student
    public const string StudentAssignments = "/api/student/assignments";
    public const string StudentAssignmentFile = "/api/student/assignments/{id}/file";
    /// <summary>PUT: submit (raw file body). GET: my submission for assignment {id}, NotFound if none.</summary>
    public const string StudentSubmission ="/api/student/assignments/{id}/submission";

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
