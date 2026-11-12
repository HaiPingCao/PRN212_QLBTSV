using System.Windows;
using QLBT.Client.Api;
using QLBT.Shared;

namespace QLBT.Client.Views.Teacher;

/// <summary>Row of the class info students grid.</summary>
public sealed class ClassStudentRow
{
    public ClassStudentDto Dto { get; init; } = new ClassStudentDto();

    public string Username
    {
        get { return Dto.Username; }
    }

    public string FullName
    {
        get { return Dto.FullName; }
    }

    public string Email
    {
        get { return Dto.Email; }
    }

    public bool IsSuspended
    {
        get { return Dto.IsSuspended; }
    }

    public string StatusText
    {
        get { return Dto.IsSuspended ? "Suspended" : "Active"; }
    }
}

/// <summary>Read-only details of one class: course, semester and students (GetClassStudents).</summary>
public sealed partial class ClassInfoDialog : Window
{
    private readonly ClassDto _class;
    private List<ClassStudentRow> _students = new List<ClassStudentRow>();

    public ClassInfoDialog(Window owner, ClassDto cls)
    {
        InitializeComponent();
        Owner = owner;
        _class = cls;
        Title = "Class " + cls.Name;
        HeaderText.Text = "Class " + cls.Name;
        CourseText.Text = cls.CourseCode + " – " + cls.CourseName;
        SemesterText.Text = cls.Semester;
        CountText.Text = cls.StudentCount + " active";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApiResult<List<ClassStudentDto>> result = await QlbtApi.GetClassStudents(_class.Id);
        if (!UiHelper.Check(this, result))
        {
            EmptyText.Text = result.Message;
            return;
        }

        _students = (result.Body ?? new List<ClassStudentDto>()).Select(s => new ClassStudentRow { Dto = s }).ToList();
        int suspended = _students.Count(s => s.IsSuspended);
        CountText.Text = (_students.Count - suspended) + " active / " + _students.Count;
        FooterText.Text = _students.Count + " students · " + suspended + " suspended";
        ApplyFilter();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        string text = SearchBox.Text.Trim();
        List<ClassStudentRow> visible = _students
            .Where(s => SuspendedOnlyBox.IsChecked != true || s.IsSuspended)
            .Where(s => text.Length == 0
                || s.Username.Contains(text, StringComparison.OrdinalIgnoreCase)
                || s.FullName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || s.Email.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        StudentsGrid.ItemsSource = visible;
        EmptyText.Text = _students.Count == 0 ? "No students in this class." : "No student matches.";
        EmptyText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
