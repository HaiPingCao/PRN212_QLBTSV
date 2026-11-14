using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QLBT.Server.Admin;
using QLBT.Server.Domain;

namespace QLBT.Server.Views;

/// <summary>
/// Classes tab: class editor on top, classes list, then the students of the selected class.
/// A class never changes its semester.
/// </summary>
public sealed partial class ClassesView : UserControl
{
    private const int AllSemesters = 0;

    private readonly ClassAdminService _service = new ClassAdminService();
    private readonly SemesterAdminService _semesterService = new SemesterAdminService();
    private readonly CourseAdminService _courseService = new CourseAdminService();
    private List<SemesterRow> _semesters = new List<SemesterRow>();
    private List<CourseRow> _courses = new List<CourseRow>();
    private List<UserRow> _teachers = new List<UserRow>();
    private List<ClassRow> _classes = new List<ClassRow>();
    private List<ClassStudentRow> _students = new List<ClassStudentRow>();
    private ICollectionView? _classView;
    private ICollectionView? _studentView;
    private ClassRow? _editing; // null = Add mode
    private bool _loaded;
    private bool _fillingFilter; // the semester filter is being set by code: don't reload

    public ClassesView()
    {
        InitializeComponent();
        ClearEditor();
        ShowStudents();
    }

    private async void View_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        await ReloadLookups();
        await ReloadClasses(null);
    }

    // ───────────────────────── Loading ─────────────────────────

    /// <summary>Semester, course and teacher pickers. Changes made in the other tabs show up after Refresh.</summary>
    private async Task ReloadLookups()
    {
        bool ok = await ViewHelper.TryRun(Root, async () =>
        {
            _semesters = await _semesterService.List();
            _courses = await _courseService.List();
            _teachers = await _service.ListTeachers();
        });
        if (!ok)
        {
            return;
        }

        int? filterId = (SemesterFilter.SelectedItem as SemesterRow)?.Id;
        List<SemesterRow> filterItems = new List<SemesterRow> { new SemesterRow { Id = AllSemesters, Name = "All semesters" } };
        filterItems.AddRange(_semesters);
        _fillingFilter = true;
        SemesterFilter.ItemsSource = filterItems;
        // Keep the current pick on refresh; "All semesters" by default.
        SemesterFilter.SelectedItem = filterItems.FirstOrDefault(s => s.Id == filterId) ?? filterItems[0];
        _fillingFilter = false;

        // Keep the current picks (by id) after replacing the lists.
        int? semesterId = (SemesterBox.SelectedItem as SemesterRow)?.Id;
        SemesterBox.ItemsSource = _semesters;
        SemesterBox.SelectedItem = _semesters.FirstOrDefault(s => s.Id == semesterId);

        int? courseId = (CourseBox.SelectedItem as CourseRow)?.Id;
        CourseBox.ItemsSource = _courses;
        CourseBox.SelectedItem = _courses.FirstOrDefault(c => c.Id == courseId);

        int? teacherId = (TeacherBox.SelectedItem as UserRow)?.Id;
        TeacherBox.ItemsSource = _teachers;
        TeacherBox.SelectedItem = _teachers.FirstOrDefault(t => t.Id == teacherId);
    }

    /// <summary>Loads the classes of the filtered semester and selects <paramref name="selectId"/> (Add mode when null or not shown).</summary>
    private async Task ReloadClasses(int? selectId)
    {
        int filterId = (SemesterFilter.SelectedItem as SemesterRow)?.Id ?? AllSemesters;
        _classes = await ViewHelper.Run(Root, () => _service.ListClasses(filterId == AllSemesters ? null : filterId)) ?? _classes;
        ClassesGrid.ItemsSource = _classes;
        _classView = CollectionViewSource.GetDefaultView(_classes);
        _classView.Filter = ClassMatches;

        ClassRow? selected = _classes.FirstOrDefault(c => c.Id == selectId);
        if (selected != null)
        {
            ClassesGrid.SelectedItem = selected; // ClassesGrid_SelectionChanged fills the editor and the students
            ClassesGrid.ScrollIntoView(selected);
        }
        else
        {
            ClearEditor();
            await ReloadStudents();
        }
    }

    private async Task ReloadStudents()
    {
        int? selectedStudent = (StudentsGrid.SelectedItem as ClassStudentRow)?.StudentId;
        _students = new List<ClassStudentRow>();
        if (_editing != null)
        {
            int classId = _editing.Id;
            _students = await ViewHelper.Run(Root, () => _service.ListStudents(classId)) ?? _students;
        }

        StudentsGrid.ItemsSource = _students;
        _studentView = CollectionViewSource.GetDefaultView(_students);
        _studentView.Filter = StudentMatches;
        StudentsGrid.SelectedItem = _students.FirstOrDefault(s => s.StudentId == selectedStudent);
        ShowStudents();
    }

    // ───────────────────────── Class list and editor ─────────────────────────

    private async void SemesterFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded && !_fillingFilter)
        {
            await ReloadClasses(_editing?.Id);
        }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _classView?.Refresh();
    }

    private async void ClassesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClassesGrid.SelectedItem is ClassRow row)
        {
            FillEditor(row);
            await ReloadStudents();
        }
    }

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        ClassesGrid.SelectedItem = null;
        ClearEditor();
        await ReloadStudents();
        NameBox.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (CourseBox.SelectedItem is not CourseRow course)
        {
            ViewHelper.SetMessage(ErrorText, "Please select a course.");
            return;
        }
        if (TeacherBox.SelectedItem is not UserRow teacher)
        {
            ViewHelper.SetMessage(ErrorText, "Please select a teacher.");
            return;
        }
        SemesterRow? semester = SemesterBox.SelectedItem as SemesterRow;
        if (_editing == null && semester == null)
        {
            ViewHelper.SetMessage(ErrorText, "Please select a semester.");
            return;
        }

        AdminResult? result = await ViewHelper.Run(Root, () => _editing == null
            ? _service.Create(semester!.Id, course.Id, NameBox.Text, teacher.Id)
            : _service.Update(_editing.Id, course.Id, NameBox.Text, teacher.Id));
        if (result == null)
        {
            return;
        }
        if (!result.Ok)
        {
            ViewHelper.SetMessage(ErrorText, result.Error);
            return;
        }
        await ReloadClasses(result.Id);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null)
        {
            return;
        }

        ClassRow row = _editing;
        int assignments = 0;
        int submissions = 0;
        bool counted = await ViewHelper.TryRun(Root, async () =>
        {
            (assignments, submissions) = await _service.CountContents(row.Id);
        });
        if (!counted)
        {
            return;
        }
        string message = "Are you sure you want to delete class " + row.Name + " · " + row.CourseCode + " (" + row.SemesterName + ")?\n\nThis will also permanently delete "
            + row.TotalCount + " enrolment(s), " + assignments + " assignment(s), " + submissions + " submission(s) and their files. This action cannot be undone.";
        if (!ViewHelper.Confirm(this, message))
        {
            return;
        }

        AdminResult? result = await ViewHelper.Run(Root, () => _service.Delete(row.Id));
        if (result == null)
        {
            return;
        }
        if (!result.Ok)
        {
            ViewHelper.Warn(this, result.Error!);
            return;
        }
        await ReloadClasses(null);
    }

    private void Assignments_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null)
        {
            return;
        }
        ClassAssignmentsDialog dialog = new ClassAssignmentsDialog(_editing, _service);
        dialog.Owner = Window.GetWindow(this);
        dialog.ShowDialog();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ReloadLookups();
        await ReloadClasses(_editing?.Id);
    }

    /// <summary>Edit mode: the semester is shown but cannot be changed.</summary>
    private void FillEditor(ClassRow row)
    {
        _editing = row;
        EditorTitle.Text = "Edit class " + row.Name + " · " + row.CourseCode;
        SemesterBox.Visibility = Visibility.Collapsed;
        SemesterLockedText.Visibility = Visibility.Visible;
        SemesterLockedText.Text = row.SemesterName;
        CourseBox.SelectedItem = _courses.FirstOrDefault(c => c.Id == row.CourseId);
        TeacherBox.SelectedItem = _teachers.FirstOrDefault(t => t.Id == row.TeacherId);
        NameBox.Text = row.Name;
        DeleteButton.Visibility = Visibility.Visible;
        AssignmentsButton.Visibility = Visibility.Visible;
        ViewHelper.SetMessage(ErrorText, null);
    }

    /// <summary>Add mode: the semester picker starts on the semester chosen in the filter.</summary>
    private void ClearEditor()
    {
        _editing = null;
        EditorTitle.Text = "New class";
        SemesterBox.Visibility = Visibility.Visible;
        SemesterLockedText.Visibility = Visibility.Collapsed;
        int? filterId = (SemesterFilter.SelectedItem as SemesterRow)?.Id;
        SemesterBox.SelectedItem = _semesters.FirstOrDefault(s => s.Id == filterId) ?? _semesters.FirstOrDefault();
        CourseBox.SelectedItem = null;
        TeacherBox.SelectedItem = null;
        NameBox.Clear();
        DeleteButton.Visibility = Visibility.Collapsed;
        AssignmentsButton.Visibility = Visibility.Collapsed;
        ViewHelper.SetMessage(ErrorText, null);
    }

    private bool ClassMatches(object item)
    {
        ClassRow row = (ClassRow)item;
        string search = SearchBox.Text.Trim();
        return search.Length == 0
            || row.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.CourseCode.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.CourseName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.TeacherDisplay.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    // ───────────────────────── Students of the selected class ─────────────────────────

    private void StudentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStudentButtons();
    }

    private void StudentFilter_Changed(object sender, RoutedEventArgs e)
    {
        _studentView?.Refresh();
    }

    private async void AddStudents_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null)
        {
            return;
        }
        AddStudentsDialog dialog = new AddStudentsDialog(_editing, _service);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            await ReloadClasses(_editing.Id);
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null || StudentsGrid.SelectedItem is not ClassStudentRow student)
        {
            return;
        }
        if (!ViewHelper.Confirm(this, "Are you sure you want to remove " + student.Username + " (" + student.FullName + ") from class " + _editing.Name + "?"))
        {
            return;
        }
        await RunStudentAction(() => _service.RemoveStudent(_editing.Id, student.StudentId));
    }

    private async void Suspend_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null || StudentsGrid.SelectedItem is not ClassStudentRow student)
        {
            return;
        }
        string message = "Are you sure you want to suspend " + student.Username + " in " + _editing.Name + " · " + _editing.CourseCode + "?\n\nThe student will no longer be able to submit work or download assignments "
            + "for this class, but their scores, comments and previous submissions will be retained.";
        if (!ViewHelper.Confirm(this, message))
        {
            return;
        }
        await RunStudentAction(() => _service.SetSuspended(_editing.Id, student.StudentId, true));
    }

    private async void Unsuspend_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null || StudentsGrid.SelectedItem is not ClassStudentRow student)
        {
            return;
        }
        await RunStudentAction(() => _service.SetSuspended(_editing.Id, student.StudentId, false));
    }

    /// <summary>Runs a student action, shows its rule error, then reloads (the class counts change too).</summary>
    private async Task RunStudentAction(Func<Task<AdminResult>> action)
    {
        AdminResult? result = await ViewHelper.Run(Root, action);
        if (result == null)
        {
            return;
        }
        if (!result.Ok)
        {
            ViewHelper.Warn(this, result.Error!);
        }
        await ReloadClasses(_editing?.Id);
    }

    private bool StudentMatches(object item)
    {
        ClassStudentRow row = (ClassStudentRow)item;
        if (SuspendedOnlyBox.IsChecked == true && row.IsActive)
        {
            return false;
        }
        string search = StudentSearchBox.Text.Trim();
        return search.Length == 0
            || row.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Email.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void ShowStudents()
    {
        StudentsTitle.Text = _editing == null
            ? "Students (select a class)"
            : "Students of " + _editing.Name + " · " + _editing.CourseCode + " (" + _editing.SemesterName + ")";
        UpdateStudentButtons();
    }

    private void UpdateStudentButtons()
    {
        ClassStudentRow? student = StudentsGrid.SelectedItem as ClassStudentRow;
        AddStudentsButton.IsEnabled = _editing != null;
        RemoveButton.IsEnabled = student != null;
        SuspendButton.IsEnabled = student != null && student.IsActive;
        UnsuspendButton.IsEnabled = student != null && !student.IsActive;
    }
}
