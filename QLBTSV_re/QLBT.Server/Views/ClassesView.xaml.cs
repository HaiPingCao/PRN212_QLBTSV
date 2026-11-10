using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QLBT.Server.Admin;
using QLBT.Server.Domain;

namespace QLBT.Server.Views;

/// <summary>
/// Classes tab: class editor on top, classes list, then the students of the selected class.
/// Editing, deleting, adding and removing students are locked once the semester has started;
/// suspend / un-suspend is always allowed.
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
    private ClassRow? _editing;   // null = Add mode
    private bool _loaded;
    private bool _filling = true; // set by code (or still initializing): ignore change events
    private bool _dirty;

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
        await ReloadClasses(null, refillEditor: true);
    }

    /// <summary>Semesters, courses and teachers may have changed in the other tabs: refresh the pickers when the tab is shown again.</summary>
    private async void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_loaded && IsVisible)
        {
            await ReloadLookups();
            await ReloadClasses(_editing?.Id, refillEditor: false);
        }
    }

    // ───────────────────────── Loading ─────────────────────────

    private async Task ReloadLookups()
    {
        try
        {
            _semesters = await _semesterService.List();
            _courses = await _courseService.List();
            _teachers = await _service.ListTeachers();
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
            return;
        }

        int? filterId = (SemesterFilter.SelectedItem as SemesterRow)?.Id;
        List<SemesterRow> filterItems = new List<SemesterRow> { new SemesterRow { Id = AllSemesters, Name = "All semesters" } };
        filterItems.AddRange(_semesters);

        _filling = true;
        SemesterFilter.ItemsSource = filterItems;
        SemesterRow? keep = filterId == null ? null : filterItems.FirstOrDefault(s => s.Id == filterId);
        // Default: the running semester, else the newest one.
        SemesterFilter.SelectedItem = keep
            ?? _semesters.FirstOrDefault(s => s.Status == SemesterStatus.Running)
            ?? _semesters.FirstOrDefault()
            ?? filterItems[0];

        int? teacherId = (TeacherBox.SelectedItem as UserRow)?.Id;
        TeacherBox.ItemsSource = _teachers;
        TeacherBox.SelectedItem = _teachers.FirstOrDefault(t => t.Id == teacherId);

        int? courseId = (CourseBox.SelectedItem as CourseRow)?.Id;
        CourseBox.ItemsSource = _courses;
        CourseBox.SelectedItem = _courses.FirstOrDefault(c => c.Id == courseId);

        int? semesterId = (SemesterBox.SelectedItem as SemesterRow)?.Id;
        List<SemesterRow> open = _semesters.Where(s => !s.HasStarted).ToList();
        SemesterBox.ItemsSource = open;
        SemesterBox.SelectedItem = open.FirstOrDefault(s => s.Id == semesterId);
        _filling = false;

        if (_editing == null)
        {
            ApplyAddModeRules();
        }
    }

    /// <param name="selectId">Class to select after loading.</param>
    /// <param name="refillEditor">false keeps unsaved editor fields (e.g. after a student action that only changed counts).</param>
    private async Task ReloadClasses(int? selectId, bool refillEditor)
    {
        int filterId = (SemesterFilter.SelectedItem as SemesterRow)?.Id ?? AllSemesters;
        SetBusy(true);
        try
        {
            _classes = await _service.ListClasses(filterId == AllSemesters ? null : filterId);
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
        }
        finally
        {
            SetBusy(false);
        }

        _filling = true;
        ClassesGrid.ItemsSource = _classes;
        _classView = CollectionViewSource.GetDefaultView(_classes);
        _classView.Filter = ClassMatches;
        ClassRow? selected = selectId == null ? null : _classes.FirstOrDefault(c => c.Id == selectId);
        ClassesGrid.SelectedItem = selected;
        if (selected != null)
        {
            ClassesGrid.ScrollIntoView(selected);
        }
        _filling = false;

        if (selected != null)
        {
            _editing = selected; // fresh row (counts may have changed)
            if (refillEditor || !_dirty)
            {
                FillEditor();
            }
        }
        UpdateClassFooter();
        await ReloadStudents();
    }

    private async Task ReloadStudents()
    {
        int? selectedStudent = (StudentsGrid.SelectedItem as ClassStudentRow)?.StudentId;
        _students = new List<ClassStudentRow>();
        if (_editing != null)
        {
            try
            {
                _students = await _service.ListStudents(_editing.Id);
            }
            catch (Exception ex)
            {
                ViewHelper.ShowError(this, ex);
            }
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
        if (_filling || !_loaded)
        {
            return;
        }
        // The edited class stays in the editor even if the new filter hides it.
        await ReloadClasses(_editing?.Id, refillEditor: false);
        if (_editing == null)
        {
            ApplyAddModeRules();
        }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _classView?.Refresh();
        UpdateClassFooter();
    }

    private async void ClassesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_filling || ClassesGrid.SelectedItem is not ClassRow row || row.Id == _editing?.Id)
        {
            return;
        }
        if (!ViewHelper.ConfirmDiscard(this, _dirty))
        {
            _filling = true;
            ClassesGrid.SelectedItem = _editing == null ? null : _classes.FirstOrDefault(c => c.Id == _editing.Id);
            _filling = false;
            return;
        }
        _editing = row;
        FillEditor();
        await ReloadStudents();
    }

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        await StartNew();
    }

    private async Task StartNew()
    {
        if (!ViewHelper.ConfirmDiscard(this, _dirty))
        {
            return;
        }
        _filling = true;
        ClassesGrid.SelectedItem = null;
        _filling = false;
        ClearEditor();
        await ReloadStudents();
        NameBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_editing != null)
        {
            FillEditor();
        }
        else
        {
            ClearEditor();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await Save();
    }

    private async Task Save()
    {
        if (!SaveButton.IsEnabled)
        {
            return;
        }
        if (CourseBox.SelectedItem is not CourseRow course)
        {
            ViewHelper.SetMessage(ErrorText, "Choose a course.");
            return;
        }
        if (TeacherBox.SelectedItem is not UserRow teacher)
        {
            ViewHelper.SetMessage(ErrorText, "Choose a teacher.");
            return;
        }

        AdminResult result;
        SetBusy(true);
        try
        {
            if (_editing == null)
            {
                if (SemesterBox.SelectedItem is not SemesterRow semester)
                {
                    ViewHelper.SetMessage(ErrorText, "Choose a semester.");
                    return;
                }
                result = await _service.Create(semester.Id, course.Id, NameBox.Text, teacher.Id);
            }
            else
            {
                result = await _service.Update(_editing.Id, course.Id, NameBox.Text, teacher.Id);
            }
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
            return;
        }
        finally
        {
            SetBusy(false);
        }

        if (!result.Ok)
        {
            ViewHelper.SetMessage(ErrorText, result.Error);
            return;
        }
        _dirty = false;
        await ReloadClasses(result.Id, refillEditor: true);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        await Delete();
    }

    private async Task Delete()
    {
        if (_editing == null || !DeleteButton.IsEnabled)
        {
            return;
        }

        AdminResult result;
        SetBusy(true);
        try
        {
            (int assignments, int submissions) = await _service.CountContents(_editing.Id);
            string message = "Delete class " + _editing.Name + " · " + _editing.CourseCode + " (" + _editing.SemesterName + ")?\n\nThis also deletes "
                + _editing.TotalCount + " enrolment(s), " + assignments + " assignment(s), " + submissions + " submission(s) and their files.";
            if (!ViewHelper.Confirm(this, message))
            {
                return;
            }
            result = await _service.Delete(_editing.Id);
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
            return;
        }
        finally
        {
            SetBusy(false);
        }

        if (!result.Ok)
        {
            ViewHelper.Warn(this, result.Error!);
            return;
        }
        ClearEditor();
        await ReloadClasses(null, refillEditor: true);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await Refresh();
    }

    private async Task Refresh()
    {
        if (!ViewHelper.ConfirmDiscard(this, _dirty))
        {
            return;
        }
        _dirty = false;
        await ReloadLookups();
        await ReloadClasses(_editing?.Id, refillEditor: true);
    }

    private void Field_Changed(object sender, RoutedEventArgs e)
    {
        if (_filling)
        {
            return;
        }
        _dirty = true;
        DirtyText.Visibility = Visibility.Visible;
        ViewHelper.SetMessage(ErrorText, null);
    }

    private async void View_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            e.Handled = true;
            await StartNew();
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            e.Handled = true;
            await Save();
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            await Refresh();
        }
    }

    private async void ClassesGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await Delete();
        }
    }

    /// <summary>Edit mode. A class whose semester has started is shown read-only.</summary>
    private void FillEditor()
    {
        ClassRow row = _editing!;
        _filling = true;
        EditorTitle.Text = "Edit class " + row.Name + " · " + row.CourseCode;
        SemesterBox.Visibility = Visibility.Collapsed;
        SemesterLockedText.Visibility = Visibility.Visible;
        SemesterLockedText.Text = row.SemesterName + "  (locked)";
        CourseBox.SelectedItem = _courses.FirstOrDefault(c => c.Id == row.CourseId);
        TeacherBox.SelectedItem = _teachers.FirstOrDefault(t => t.Id == row.TeacherId);
        NameBox.Text = row.Name;
        _filling = false;

        bool started = row.HasStarted;
        SetFieldsEnabled(!started);
        SaveButton.IsEnabled = !started;
        CancelButton.IsEnabled = !started;
        DeleteButton.Visibility = Visibility.Visible;
        DeleteButton.IsEnabled = !started;
        string lockReason = "Semester " + row.SemesterName + " has started";
        SaveButton.ToolTip = started ? lockReason : "Save (Ctrl+S)";
        DeleteButton.ToolTip = started ? lockReason : "Delete this class with its assignments and submissions (Del)";
        ViewHelper.SetMessage(InfoText, started
            ? "ℹ " + row.SemesterName + " has started: the class cannot be edited or deleted and students cannot be added or removed. Suspension is still allowed."
            : null);
        ResetState();
    }

    /// <summary>Add mode: semester picker limited to semesters that have not started.</summary>
    private void ClearEditor()
    {
        _editing = null;
        _filling = true;
        EditorTitle.Text = "New class";
        SemesterBox.Visibility = Visibility.Visible;
        SemesterLockedText.Visibility = Visibility.Collapsed;
        CourseBox.SelectedItem = null;
        TeacherBox.SelectedItem = null;
        NameBox.Clear();
        _filling = false;

        DeleteButton.Visibility = Visibility.Collapsed;
        CancelButton.IsEnabled = true;
        SaveButton.ToolTip = "Save (Ctrl+S)";
        ApplyAddModeRules();
        ResetState();
    }

    /// <summary>Preselects a usable semester; disables Save when every semester has started or there is no course yet.</summary>
    private void ApplyAddModeRules()
    {
        List<SemesterRow> open = SemesterBox.ItemsSource as List<SemesterRow> ?? new List<SemesterRow>();
        if (SemesterBox.SelectedItem == null)
        {
            int? filterId = (SemesterFilter.SelectedItem as SemesterRow)?.Id;
            _filling = true;
            SemesterBox.SelectedItem = open.FirstOrDefault(s => s.Id == filterId) ?? open.FirstOrDefault();
            _filling = false;
        }

        string? blocked = null;
        if (open.Count == 0)
        {
            blocked = "ℹ Every semester has started. Create a new semester in the Semesters tab before adding classes.";
        }
        else if (_courses.Count == 0)
        {
            blocked = "ℹ There is no course yet. Create one in the Courses tab before adding classes.";
        }
        bool canAdd = blocked == null;
        SetFieldsEnabled(canAdd);
        SaveButton.IsEnabled = canAdd;
        ViewHelper.SetMessage(InfoText, blocked);
    }

    private void SetFieldsEnabled(bool enabled)
    {
        SemesterBox.IsEnabled = enabled;
        CourseBox.IsEnabled = enabled;
        TeacherBox.IsEnabled = enabled;
        NameBox.IsReadOnly = !enabled;
    }

    private void ResetState()
    {
        _dirty = false;
        DirtyText.Visibility = Visibility.Collapsed;
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

    private void UpdateClassFooter()
    {
        int shown = ClassesGrid.Items.Count;
        ClassFooterText.Text = shown == _classes.Count ? _classes.Count + " classes" : shown + " of " + _classes.Count + " classes";
    }

    // ───────────────────────── Students of the selected class ─────────────────────────

    private void StudentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStudentButtons();
    }

    private void StudentFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (_studentView == null)
        {
            return;
        }
        _studentView.Refresh();
        ShowStudents();
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
            await ReloadClasses(_editing.Id, refillEditor: false);
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null || StudentsGrid.SelectedItem is not ClassStudentRow student)
        {
            return;
        }
        if (!ViewHelper.Confirm(this, "Remove " + student.Username + " (" + student.FullName + ") from class " + _editing.Name + "?"))
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
        string message = "Suspend " + student.Username + " in " + _editing.Name + " · " + _editing.CourseCode + "?\n\nThey can no longer submit or download assignments "
            + "of this class, but keep their scores, comments and old submissions.";
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

    private async Task RunStudentAction(Func<Task<AdminResult>> action)
    {
        AdminResult result;
        SetBusy(true);
        try
        {
            result = await action();
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
            return;
        }
        finally
        {
            SetBusy(false);
        }

        if (!result.Ok)
        {
            ViewHelper.Warn(this, result.Error!);
        }
        await ReloadClasses(_editing?.Id, refillEditor: false);
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
        if (_editing == null)
        {
            StudentsTitle.Text = "Students";
            StudentsEmptyText.Text = "Select a class to see its students.";
            StudentsEmptyText.Visibility = Visibility.Visible;
            StudentFooterText.Text = "";
        }
        else
        {
            StudentsTitle.Text = "Students of " + _editing.Name + " · " + _editing.CourseCode + " (" + _editing.SemesterName + " · " + _editing.TeacherUsername + ")";
            StudentsEmptyText.Text = _students.Count == 0 ? "No students in this class yet." : "";
            StudentsEmptyText.Visibility = _students.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            int suspended = _students.Count(s => !s.IsActive);
            int shown = StudentsGrid.Items.Count;
            string text = _students.Count + " students · " + suspended + " suspended";
            StudentFooterText.Text = shown == _students.Count ? text : "Showing " + shown + " of " + text;
        }
        UpdateStudentButtons();
    }

    private void UpdateStudentButtons()
    {
        ClassStudentRow? student = StudentsGrid.SelectedItem as ClassStudentRow;
        bool hasClass = _editing != null;
        bool started = _editing?.HasStarted == true;
        string lockReason = "Semester " + _editing?.SemesterName + " has started: suspend instead";

        AddStudentsButton.IsEnabled = hasClass && !started;
        AddStudentsButton.ToolTip = started ? "Semester " + _editing!.SemesterName + " has started: students cannot be added" : null;
        RemoveButton.IsEnabled = student != null && !started;
        RemoveButton.ToolTip = started ? lockReason : "Remove the student from this class";
        SuspendButton.IsEnabled = student != null && student.IsActive;
        UnsuspendButton.IsEnabled = student != null && !student.IsActive;
    }

    private void SetBusy(bool busy)
    {
        Root.IsEnabled = !busy;
        if (busy)
        {
            ClassFooterText.Text = "Loading...";
        }
        else
        {
            UpdateClassFooter();
        }
    }
}
