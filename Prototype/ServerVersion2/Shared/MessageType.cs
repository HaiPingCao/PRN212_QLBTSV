namespace Tcp.Shared;

/// <summary>Operation carried by a request. The response echoes the same value.</summary>
public enum MessageType
{
    // Both roles
    Login,
    Logout,
    GetProfile,
    DownloadSubmission,

    // Teacher
    GetMyClasses,
    GetClassStudents,
    CreateAssignment,
    UpdateAssignment,
    DeleteAssignment,
    GetSubmissions,
    GradeSubmission,
    DeleteSubmission,

    // Student
    GetMyAssignments,
    DownloadAssignment,
    SubmitAssignment,
    GetMySubmission,

    // Test only, no login needed. Remove when the real handlers are done.
    Echo,       // replies with the request body
    EchoFile    // receives a file and sends it back
}
