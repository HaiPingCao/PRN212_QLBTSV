using QLBT.Shared;

namespace QLBT.Client.Views.Teacher;

/// <summary>Class list entry: only the class code, plus the course when the same code is taught twice in a semester.</summary>
public sealed class TeacherClassItem
{
    public ClassDto Dto { get; init; } = new ClassDto();

    public string Display { get; init; } = "";

    public string ToolTip
    {
        get { return Dto.CourseCode + " – " + Dto.CourseName + "\n" + Dto.Semester + " · " + Dto.StudentCount + " active students"; }
    }
}

/// <summary>Row of the assignments grid.</summary>
public sealed class AssignmentRow
{
    public TeacherAssignmentDto Dto { get; init; } = new TeacherAssignmentDto();

    /// <summary>Active students of the class, for "submitted / students".</summary>
    public int StudentCount { get; init; }

    public string Title
    {
        get { return Dto.Title; }
    }

    public DateTime DueDate
    {
        get { return Dto.DueDate; }
    }

    public string DueText
    {
        get { return Dto.DueDate.ToString(UiHelper.DateTimeFormat); }
    }

    public bool IsClosed
    {
        get { return Dto.IsClosed; }
    }

    public string StatusText
    {
        get
        {
            if (Dto.IsClosedManually)
            {
                return "Closed (manual)";
            }
            if (Dto.IsClosed)
            {
                return "Closed";
            }
            return Dto.AllowLate && DateTime.Now > Dto.DueDate ? "Open (late)" : "Open";
        }
    }

    public string SubmittedText
    {
        get { return Dto.SubmissionCount + " / " + StudentCount; }
    }

    public int GradedCount
    {
        get { return Dto.GradedCount; }
    }

    public string FileText
    {
        get { return Dto.FileName ?? ""; }
    }
}

/// <summary>Row of the submissions grid.</summary>
public sealed class SubmissionRow
{
    public SubmissionDto Dto { get; init; } = new SubmissionDto();

    public int Id
    {
        get { return Dto.Id; }
    }

    public string StudentUsername
    {
        get { return Dto.StudentUsername; }
    }

    public string StudentName
    {
        get { return Dto.StudentName; }
    }

    public DateTime SubmittedAt
    {
        get { return Dto.SubmittedAt; }
    }

    public string SubmittedText
    {
        get { return Dto.SubmittedAt.ToString(UiHelper.DateTimeFormat); }
    }

    public bool IsLate
    {
        get { return Dto.IsLate; }
    }

    public string LateText
    {
        get { return Dto.IsLate ? "Late" : ""; }
    }

    public int SubmitCount
    {
        get { return Dto.SubmitCount; }
    }

    public bool IsGraded
    {
        get { return Dto.Score != null; }
    }

    public string ScoreText
    {
        get { return UiHelper.FormatScore(Dto.Score); }
    }

    public string FileName
    {
        get { return Dto.FileName; }
    }
}
