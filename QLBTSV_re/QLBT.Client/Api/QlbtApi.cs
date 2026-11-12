using QLBT.Shared;

namespace QLBT.Client.Api;

/// <summary>One typed call per operation of Plan.md, on the logged-in session's client.</summary>
public static class QlbtApi
{
    private static ApiClient Client
    {
        get { return Session.Client; }
    }

    // Both roles

    public static Task<ApiResult<UserDto>> GetProfile()
    {
        return Client.Get<UserDto>(ApiRoutes.Me);
    }

    public static Task<ApiResult<DownloadedFile>> DownloadSubmission(int submissionId, Func<string, string?> chooseTarget)
    {
        return Client.Download(ApiRoutes.WithId(ApiRoutes.SubmissionFile, submissionId), chooseTarget);
    }

    // Teacher

    public static Task<ApiResult<List<ClassDto>>> GetMyClasses()
    {
        return Client.Get<List<ClassDto>>(ApiRoutes.TeacherClasses);
    }

    public static Task<ApiResult<List<ClassStudentDto>>> GetClassStudents(int classId)
    {
        return Client.Get<List<ClassStudentDto>>(ApiRoutes.WithId(ApiRoutes.TeacherClassStudents, classId));
    }

    public static Task<ApiResult<List<TeacherAssignmentDto>>> GetClassAssignments(int classId)
    {
        return Client.Get<List<TeacherAssignmentDto>>(ApiRoutes.WithId(ApiRoutes.TeacherClassAssignments, classId));
    }

    /// <summary>Creates the assignment without a file; upload it next with <see cref="UploadAssignmentFile"/>.</summary>
    public static Task<ApiResult<CreatedResult>> CreateAssignment(int classId, AssignmentEditRequest request)
    {
        return Client.Post<CreatedResult>(ApiRoutes.WithId(ApiRoutes.TeacherClassAssignments, classId), request);
    }

    public static Task<ApiResult<object>> UpdateAssignment(int assignmentId, AssignmentEditRequest request)
    {
        return Client.Put(ApiRoutes.WithId(ApiRoutes.TeacherAssignment, assignmentId), request);
    }

    public static Task<ApiResult<object>> DeleteAssignment(int assignmentId)
    {
        return Client.Delete(ApiRoutes.WithId(ApiRoutes.TeacherAssignment, assignmentId));
    }

    public static Task<ApiResult<UploadResult>> UploadAssignmentFile(int assignmentId, string localFile)
    {
        return Client.UploadFile<UploadResult>(ApiRoutes.WithId(ApiRoutes.TeacherAssignmentFile, assignmentId), localFile);
    }

    public static Task<ApiResult<DownloadedFile>> DownloadTeacherAssignmentFile(int assignmentId, Func<string, string?> chooseTarget)
    {
        return Client.Download(ApiRoutes.WithId(ApiRoutes.TeacherAssignmentFile, assignmentId), chooseTarget);
    }

    public static Task<ApiResult<List<SubmissionDto>>> GetSubmissions(int assignmentId)
    {
        return Client.Get<List<SubmissionDto>>(ApiRoutes.WithId(ApiRoutes.TeacherSubmissions, assignmentId));
    }

    public static Task<ApiResult<object>> GradeSubmission(int submissionId, GradeRequest request)
    {
        return Client.Put(ApiRoutes.WithId(ApiRoutes.TeacherSubmissionGrade, submissionId), request);
    }

    public static Task<ApiResult<object>> DeleteSubmission(int submissionId)
    {
        return Client.Delete(ApiRoutes.WithId(ApiRoutes.TeacherSubmission, submissionId));
    }

    // Student

    public static Task<ApiResult<List<StudentAssignmentDto>>> GetMyAssignments()
    {
        return Client.Get<List<StudentAssignmentDto>>(ApiRoutes.StudentAssignments);
    }

    public static Task<ApiResult<DownloadedFile>> DownloadAssignment(int assignmentId, Func<string, string?> chooseTarget)
    {
        return Client.Download(ApiRoutes.WithId(ApiRoutes.StudentAssignmentFile, assignmentId), chooseTarget);
    }

    public static Task<ApiResult<SubmitResult>> SubmitAssignment(int assignmentId, string localFile)
    {
        return Client.UploadFile<SubmitResult>(ApiRoutes.WithId(ApiRoutes.StudentSubmission, assignmentId), localFile);
    }

    /// <summary>NotFound when the student has not submitted yet.</summary>
    public static Task<ApiResult<MySubmissionDto>> GetMySubmission(int assignmentId)
    {
        return Client.Get<MySubmissionDto>(ApiRoutes.WithId(ApiRoutes.StudentSubmission, assignmentId));
    }
}
