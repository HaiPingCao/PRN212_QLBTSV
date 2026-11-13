using System.ComponentModel;
using QLBT.Server.Domain;
using QLBT.Shared;

namespace QLBT.Server.Admin;

/// <summary>Row of the Users grid.</summary>
public sealed class UserRow
{
    public int Id { get; init; }

    public string Username { get; init; } = "";

    public string FullName { get; init; } = "";

    public string Email { get; init; } = "";

    public UserRole Role { get; init; }

    /// <summary>Classes taught (teacher) or enrolled in (student). Users with classes cannot be deleted.</summary>
    public int ClassCount { get; init; }

    public string Display
    {
        get { return Username + " - " + FullName; }
    }
}

/// <summary>Row of the Semesters grid; also used by the semester pickers.</summary>
public sealed class SemesterRow
{
    public int Id { get; init; }

    public string Name { get; init; } = "";

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public int ClassCount { get; init; }

    public SemesterStatus Status
    {
        get { return SemesterRules.Status(StartDate, EndDate); }
    }

    public string StatusText
    {
        get
        {
            switch (Status)
            {
                case SemesterStatus.NotStarted:
                    return "Not started";
                case SemesterStatus.Running:
                    return "Running";
                default:
                    return "Ended";
            }
        }
    }
}

/// <summary>Row of the Courses grid; also used by the course picker of the class editor.</summary>
public sealed class CourseRow
{
    public int Id { get; init; }

    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    /// <summary>Courses with classes cannot be deleted.</summary>
    public int ClassCount { get; init; }

    public string Display
    {
        get { return Code + " - " + Name; }
    }
}

/// <summary>Row of the Classes grid.</summary>
public sealed class ClassRow
{
    public int Id { get; init; }

    public string Name { get; init; } = "";

    public int CourseId { get; init; }

    public string CourseCode { get; init; } = "";

    public string CourseName { get; init; } = "";

    public int SemesterId { get; init; }

    public string SemesterName { get; init; } = "";

    public int TeacherId { get; init; }

    public string TeacherUsername { get; init; } = "";

    public string TeacherName { get; init; } = "";

    public int ActiveCount { get; init; }

    public int TotalCount { get; init; }

    public string TeacherDisplay
    {
        get { return TeacherUsername + " " + TeacherName; }
    }

    public string StudentsText
    {
        get { return ActiveCount + " / " + TotalCount; }
    }
}

/// <summary>Row of the students-of-a-class grid.</summary>
public sealed class ClassStudentRow
{
    public int StudentId { get; init; }

    public string Username { get; init; } = "";

    public string FullName { get; init; } = "";

    public string Email { get; init; } = "";

    public bool IsActive { get; init; }

    public DateTime JoinedAt { get; init; }

    public DateTime? LeftAt { get; init; }

    public string StatusText
    {
        get { return IsActive ? "Active" : "Suspended"; }
    }
}

/// <summary>Row of the Add students picker. Raises PropertyChanged so "select all" updates the checkboxes.</summary>
public sealed class SelectableStudent : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; init; }

    public string Username { get; init; } = "";

    public string FullName { get; init; } = "";

    public string Email { get; init; } = "";

    public bool IsSelected
    {
        get { return _isSelected; }
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
