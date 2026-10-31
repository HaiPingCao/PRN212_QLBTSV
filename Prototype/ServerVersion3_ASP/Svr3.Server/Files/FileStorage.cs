using System.Globalization;

namespace Svr3.Server.Files;

/// <summary>
/// Builds file paths from database ids. Layout:
///
///   {root}/{semesterId}/{classId}/{assignmentId}/assignment
///   {root}/{semesterId}/{classId}/{assignmentId}/submissions/{submissionId}
/// </summary>
public sealed class FileStorage
{
    public string Root { get; }

    public FileStorage(string root)
    {
        Root = Path.GetFullPath(root);
    }

    /// <summary>Deleting an assignment = deleting this folder.</summary>
    public string AssignmentDirectory(int semesterId, int classId, int assignmentId)
    {
        return Path.Combine(Root, Id(semesterId), Id(classId), Id(assignmentId));
    }

    public string AssignmentFile(int semesterId, int classId, int assignmentId)
    {
        return Path.Combine(AssignmentDirectory(semesterId, classId, assignmentId), "assignment");
    }

    public string SubmissionsDirectory(int semesterId, int classId, int assignmentId)
    {
        return Path.Combine(AssignmentDirectory(semesterId, classId, assignmentId), "submissions");
    }

    public string SubmissionFile(int semesterId, int classId, int assignmentId, int submissionId)
    {
        return Path.Combine(SubmissionsDirectory(semesterId, classId, assignmentId), Id(submissionId));
    }

    /// <summary>Value stored in the file_path columns, e.g. "2/3/6/assignment".</summary>
    public string ToRelative(string fullPath)
    {
        return Path.GetRelativePath(Root, fullPath).Replace('\\', '/');
    }

    private static string Id(int id)
    {
        return id.ToString(CultureInfo.InvariantCulture);
    }
}
