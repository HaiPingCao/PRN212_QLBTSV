using QLBT.Shared;

namespace QLBT.Client.Views.Student;

/// <summary>Tree node: one class of the student ("PRN212 · SE1802") with its assignments.</summary>
public sealed class StudentClassGroup
{
    public string Header { get; init; } = "";

    public string ToolTip { get; init; } = "";

    public bool IsSuspended { get; init; }

    public List<StudentAssignmentItem> Items { get; init; } = new List<StudentAssignmentItem>();

    /// <summary>Bound to TreeViewItem.IsSelected (groups are never selected by code).</summary>
    public bool IsSelected { get; set; }
}

/// <summary>Tree leaf / due-date grid row: one assignment.</summary>
public sealed class StudentAssignmentItem
{
    public StudentAssignmentDto Dto { get; init; } = new StudentAssignmentDto();

    /// <summary>Bound two-way to TreeViewItem.IsSelected so a refresh can re-select the same assignment.</summary>
    public bool IsSelected { get; set; }

    public int Id
    {
        get { return Dto.Id; }
    }

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

    public string ClassDisplay
    {
        get { return Dto.CourseCode + " · " + Dto.ClassName; }
    }

    /// <summary>Greyed in the lists: closed, or the class is suspended.</summary>
    public bool IsInactive
    {
        get { return Dto.IsClosed || Dto.ClassSuspended; }
    }

    public string StatusText
    {
        get
        {
            if (Dto.ClassSuspended)
            {
                return "Suspended";
            }
            return Dto.IsClosed ? "Closed" : "Open";
        }
    }
}
