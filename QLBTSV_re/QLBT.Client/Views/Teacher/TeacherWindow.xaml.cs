using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using QLBT.Client.Api;
using QLBT.Shared;

namespace QLBT.Client.Views.Teacher;

/// <summary>
/// Teacher UI (3 panes): class codes on the left; assignments of the selected class (editor on top, list below);
/// submissions of the selected assignment at the bottom. Grading opens GradeDialog, class details ClassInfoDialog.
/// </summary>
public sealed partial class TeacherWindow : Window
{
    private List<ClassDto> _classes = new List<ClassDto>();
    private List<SubmissionRow> _submissions = new List<SubmissionRow>();
    private ClassDto? _class;
    private AssignmentRow? _editing;    // null = Add mode
    private string? _chosenFile;         // new assignment file to upload on Save
    private bool _filling;               // editor is being filled by code: not a user edit
    private bool _dirty;
    private bool _reverting;             // selection is being put back after "keep my changes"
    private object? _lastClassItem;
    private object? _lastAssignmentItem;
    private int? _keepSubmissionId;
    private string? _lastSavedPath;

    public TeacherWindow()
    {
        InitializeComponent();
        UserText.Text = Session.UserDisplay;
        Title = "QLBT – " + Session.UserDisplay;
        ClearEditor();
        ShowSubmissions(null);
        UpdateClassButtons();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadClasses();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        App.TopLevelClosed();
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            await RefreshAll();
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && SaveButton.IsEnabled)
        {
            e.Handled = true;
            Save_Click(SaveButton, new RoutedEventArgs());
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control && NewButton.IsEnabled)
        {
            e.Handled = true;
            New_Click(NewButton, new RoutedEventArgs());
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAll();
    }

    private async Task RefreshAll()
    {
        if (!UiHelper.ConfirmDiscard(this, _dirty))
        {
            return;
        }
        SetDirty(false);
        await ReloadClasses();
    }

    private void Profile_Click(object sender, RoutedEventArgs e)
    {
        new ProfileDialog(this).ShowDialog();
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (UiHelper.ConfirmDiscard(this, _dirty))
        {
            await App.Logout(this);
        }
    }

    // ---- Classes ----

    private async Task ReloadClasses()
    {
        int? keepClassId = _class?.Id;
        string? keepSemester = SemesterBox.SelectedItem as string;

        ApiResult<List<ClassDto>> result;
        using (new BusyScope(Root, StatusText, "Loading classes..."))
        {
            result = await QlbtApi.GetMyClasses();
        }
        if (!UiHelper.Check(this, result))
        {
            return;
        }
        _classes = result.Body ?? new List<ClassDto>();

        // The server orders by semester id (oldest first): newest semester first in the combo.
        List<string> semesters = _classes.Select(c => c.Semester).Distinct().Reverse().ToList();
        string? semester = keepSemester != null && semesters.Contains(keepSemester) ? keepSemester : semesters.FirstOrDefault();
        _reverting = true;
        SemesterBox.ItemsSource = semesters;
        SemesterBox.SelectedItem = semester;
        _reverting = false;

        await ShowClasses(keepClassId);
        StatusText.Text = "Loaded " + _classes.Count + " classes at " + DateTime.Now.ToString("HH:mm");
    }

    private async void SemesterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_reverting)
        {
            return;
        }
        if (!UiHelper.ConfirmDiscard(this, _dirty))
        {
            Revert(SemesterBox, e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null);
            return;
        }
        SetDirty(false);
        await ShowClasses(null);
    }

    /// <summary>Fills the class list for the selected semester and selects <paramref name="selectId"/> (or the first class).</summary>
    private async Task ShowClasses(int? selectId)
    {
        string? semester = SemesterBox.SelectedItem as string;
        List<ClassDto> inSemester = _classes.Where(c => c.Semester == semester).ToList();
        List<TeacherClassItem> items = inSemester
            .Select(c => new TeacherClassItem
            {
                Dto = c,
                // Same code taught for two courses in one semester: add the course to tell them apart.
                Display = inSemester.Count(o => o.Name == c.Name) > 1 ? c.Name + " (" + c.CourseCode + ")" : c.Name
            })
            .ToList();

        TeacherClassItem? select = items.FirstOrDefault(i => i.Dto.Id == selectId) ?? items.FirstOrDefault();
        _reverting = true;
        ClassList.ItemsSource = items;
        ClassList.SelectedItem = select;
        _reverting = false;
        _lastClassItem = select;
        ClassEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        await SelectClass(select?.Dto, _editing?.Dto.Id);
    }

    private async void ClassList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_reverting)
        {
            return;
        }
        if (!UiHelper.ConfirmDiscard(this, _dirty))
        {
            Revert(ClassList, _lastClassItem);
            return;
        }
        _lastClassItem = ClassList.SelectedItem;
        SetDirty(false);
        await SelectClass((ClassList.SelectedItem as TeacherClassItem)?.Dto, null);
    }

    private void ClassList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_class != null)
        {
            ClassInfo_Click(sender, e);
        }
    }

    private void ClassInfo_Click(object sender, RoutedEventArgs e)
    {
        if (_class != null)
        {
            new ClassInfoDialog(this, _class).ShowDialog();
        }
    }

    private async Task SelectClass(ClassDto? dto, int? selectAssignmentId)
    {
        _class = dto;
        UpdateClassButtons();
        if (dto == null)
        {
            ClassHeaderText.Text = _classes.Count == 0 ? "You do not teach any class." : "Select a class";
            AssignmentsGrid.ItemsSource = null;
            SetAssignmentsEmpty("No class selected.");
            ClearEditor();
            ShowSubmissions(null);
            return;
        }
        ClassHeaderText.Text = "Assignments of " + dto.Name + " · " + dto.CourseCode + " · " + dto.Semester
            + "   (" + dto.StudentCount + " active students)";
        await ReloadAssignments(selectAssignmentId);
    }

    private void UpdateClassButtons()
    {
        bool hasClass = _class != null;
        ClassInfoButton.IsEnabled = hasClass;
        NewButton.IsEnabled = hasClass;
        EditorGroup.IsEnabled = hasClass;
    }

    // ---- Assignments ----

    private async Task ReloadAssignments(int? selectId)
    {
        if (_class == null)
        {
            return;
        }
        ClassDto cls = _class;
        ApiResult<List<TeacherAssignmentDto>> result;
        using (new BusyScope(Root, StatusText, "Loading assignments..."))
        {
            result = await QlbtApi.GetClassAssignments(cls.Id);
        }
        if (cls != _class || !UiHelper.Check(this, result))
        {
            return;
        }

        List<AssignmentRow> rows = (result.Body ?? new List<TeacherAssignmentDto>())
            .OrderByDescending(a => a.DueDate)
            .Select(a => new AssignmentRow { Dto = a, StudentCount = cls.StudentCount })
            .ToList();
        AssignmentRow? select = rows.FirstOrDefault(r => r.Dto.Id == selectId);

        _reverting = true;
        AssignmentsGrid.ItemsSource = rows;
        AssignmentsGrid.SelectedItem = select;
        _reverting = false;
        _lastAssignmentItem = select;
        if (select != null)
        {
            AssignmentsGrid.ScrollIntoView(select);
        }
        SetAssignmentsEmpty(rows.Count == 0 ? "No assignments yet. Fill in the editor above and Save to create one." : null);
        await EditAssignment(select);
    }

    private void SetAssignmentsEmpty(string? text)
    {
        AssignmentsEmptyText.Text = text ?? "";
        AssignmentsEmptyText.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void AssignmentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_reverting)
        {
            return;
        }
        if (!UiHelper.ConfirmDiscard(this, _dirty))
        {
            Revert(AssignmentsGrid, _lastAssignmentItem);
            return;
        }
        _lastAssignmentItem = AssignmentsGrid.SelectedItem;
        await EditAssignment(AssignmentsGrid.SelectedItem as AssignmentRow);
    }

    /// <summary>Puts the previous selection back after the user chose to keep unsaved changes.</summary>
    private void Revert(Selector list, object? previous)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _reverting = true;
            list.SelectedItem = previous;
            _reverting = false;
        });
    }

    private async Task EditAssignment(AssignmentRow? row)
    {
        if (row == null)
        {
            ClearEditor();
            ShowSubmissions(null);
            return;
        }
        _editing = row;
        FillEditor();
        await ReloadSubmissions();
    }

    // ---- Editor ----

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (_class == null || !UiHelper.ConfirmDiscard(this, _dirty))
        {
            return;
        }
        _reverting = true;
        AssignmentsGrid.SelectedItem = null;
        _reverting = false;
        _lastAssignmentItem = null;
        ClearEditor();
        ShowSubmissions(null);
        TitleBox.Focus();
    }

    private void ClearEditor()
    {
        _filling = true;
        _editing = null;
        _chosenFile = null;
        EditorTitle.Text = "New assignment";
        TitleBox.Text = "";
        DescriptionBox.Text = "";
        DueDatePicker.SelectedDate = DateTime.Today.AddDays(7);
        DueTimeBox.Text = "23:59";
        AllowLateBox.IsChecked = false;
        ClosedBox.IsChecked = false;
        _filling = false;
        SetDirty(false);
        UiHelper.SetMessage(ErrorText, null);
        ApplyRules();
    }

    private void FillEditor()
    {
        if (_editing == null)
        {
            ClearEditor();
            return;
        }
        TeacherAssignmentDto a = _editing.Dto;
        _filling = true;
        _chosenFile = null;
        EditorTitle.Text = "Edit assignment \"" + a.Title + "\"";
        TitleBox.Text = a.Title;
        DescriptionBox.Text = a.Description ?? "";
        DueDatePicker.SelectedDate = a.DueDate.Date;
        DueTimeBox.Text = a.DueDate.ToString("HH:mm");
        AllowLateBox.IsChecked = a.AllowLate;
        ClosedBox.IsChecked = a.IsClosedManually;
        _filling = false;
        SetDirty(false);
        UiHelper.SetMessage(ErrorText, null);
        ApplyRules();
    }

    /// <summary>Status, file line, button states and the ℹ explanation for the current editor content.</summary>
    private void ApplyRules()
    {
        bool editing = _editing != null;
        ClosedBox.IsEnabled = editing;
        DeleteButton.IsEnabled = editing;
        CancelButton.IsEnabled = _dirty || editing;

        string? existing = _editing?.Dto.FileName;
        if (_chosenFile != null)
        {
            FileText.Text = "New: " + Path.GetFileName(_chosenFile) + " (" + UiHelper.FileSizeOf(_chosenFile) + ")"
                + (existing != null ? ", replaces " + existing : "");
            FileText.Foreground = (Brush)FindResource("StatusWarnBrush");
        }
        else
        {
            FileText.Text = existing ?? "(no file)";
            FileText.Foreground = existing != null ? SystemColors.ControlTextBrush : (Brush)FindResource("StatusMutedBrush");
        }
        ChooseFileButton.Content = existing != null || _chosenFile != null ? "Replace file…" : "Choose file…";
        DownloadFileButton.IsEnabled = existing != null;

        if (!editing)
        {
            StatusValueText.Text = "New";
            StatusValueText.Foreground = (Brush)FindResource("StatusMutedBrush");
            UiHelper.SetMessage(InfoText, "ℹ A new assignment starts open. The file can be chosen now or later.");
            return;
        }

        TeacherAssignmentDto a = _editing!.Dto;
        bool pastDue = DateTime.Now > a.DueDate;
        StatusValueText.Text = _editing.StatusText;
        StatusValueText.Foreground = (Brush)FindResource(a.IsClosed ? "StatusMutedBrush" : "StatusOkBrush");
        string info;
        if (a.IsClosedManually)
        {
            info = "Closed manually: students cannot submit. Grading is enabled. Untick \"Closed manually\" to reopen.";
        }
        else if (a.IsClosed)
        {
            info = "Past due and late submissions are not allowed: closed. Grading is enabled. Extend the due date to reopen.";
        }
        else if (pastDue)
        {
            info = "Past due but late submissions are allowed: still open. Close it manually to grade.";
        }
        else
        {
            info = "Open until " + a.DueDate.ToString(UiHelper.DateTimeFormat) + ". Grading is enabled once it is closed.";
        }
        UiHelper.SetMessage(InfoText, "ℹ " + info);
    }

    private void Field_Changed(object sender, RoutedEventArgs e)
    {
        if (!_filling && IsLoaded)
        {
            SetDirty(true);
        }
    }

    private void SetDirty(bool dirty)
    {
        _dirty = dirty;
        DirtyText.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = dirty || _editing != null;
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        string? file = UiHelper.PickFile(this, "Assignment file");
        if (file != null)
        {
            _chosenFile = file;
            SetDirty(true);
            ApplyRules();
        }
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

    /// <summary>Reads and checks the editor; null (with the red line set) when something is wrong.</summary>
    private AssignmentEditRequest? ReadEditor()
    {
        string title = TitleBox.Text.Trim();
        if (title.Length == 0)
        {
            UiHelper.SetMessage(ErrorText, "Title is required.");
            return null;
        }
        if (DueDatePicker.SelectedDate == null)
        {
            UiHelper.SetMessage(ErrorText, "Choose a due date.");
            return null;
        }
        if (!DateTime.TryParseExact(DueTimeBox.Text.Trim(), new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime time))
        {
            UiHelper.SetMessage(ErrorText, "Due time must be HH:mm, e.g. 23:59.");
            return null;
        }

        return new AssignmentEditRequest
        {
            Title = title,
            Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim(),
            DueDate = DueDatePicker.SelectedDate.Value.Date + time.TimeOfDay,
            AllowLate = AllowLateBox.IsChecked == true,
            IsClosed = _editing != null && ClosedBox.IsChecked == true
        };
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_class == null)
        {
            return;
        }
        AssignmentEditRequest? request = ReadEditor();
        if (request == null)
        {
            return;
        }
        UiHelper.SetMessage(ErrorText, null);
        if (_editing == null && request.DueDate < DateTime.Now
            && !UiHelper.Confirm(this, "The due date is in the past, so the assignment may be closed right away. Create it anyway?"))
        {
            return;
        }

        int id;
        string? uploadError = null;
        using (new BusyScope(Root, StatusText, "Saving..."))
        {
            if (_editing == null)
            {
                ApiResult<CreatedResult> created = await QlbtApi.CreateAssignment(_class.Id, request);
                if (!UiHelper.Check(this, created, ErrorText) || created.Body == null)
                {
                    return;
                }
                id = created.Body.Id;
            }
            else
            {
                id = _editing.Dto.Id;
                ApiResult<object> updated = await QlbtApi.UpdateAssignment(id, request);
                if (!UiHelper.Check(this, updated, ErrorText))
                {
                    return;
                }
            }

            if (_chosenFile != null)
            {
                StatusText.Text = "Uploading " + Path.GetFileName(_chosenFile) + " (" + UiHelper.FileSizeOf(_chosenFile) + ")...";
                ApiResult<UploadResult> upload = await QlbtApi.UploadAssignmentFile(id, _chosenFile);
                if (upload.Status == ApiStatus.Unauthorized)
                {
                    App.SessionExpired();
                    return;
                }
                if (!upload.IsOk)
                {
                    uploadError = upload.Message;
                }
            }
        }

        StatusText.Text = "Saved \"" + request.Title + "\" at " + DateTime.Now.ToString("HH:mm");
        SetDirty(false);
        await ReloadAssignments(id);
        if (uploadError != null)
        {
            UiHelper.Warn(this, "The assignment was saved, but the file upload failed:\n\n" + uploadError
                + "\n\nChoose the file again and Save to retry.");
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null)
        {
            return;
        }
        TeacherAssignmentDto a = _editing.Dto;
        string what = a.SubmissionCount > 0
            ? "This also deletes " + a.SubmissionCount + " submission(s), their scores and all files."
            : "This also deletes its file.";
        if (!UiHelper.Confirm(this, "Delete \"" + a.Title + "\"?\n\n" + what))
        {
            return;
        }

        ApiResult<object> result;
        using (new BusyScope(Root, StatusText, "Deleting..."))
        {
            result = await QlbtApi.DeleteAssignment(a.Id);
        }
        if (!UiHelper.Check(this, result))
        {
            return;
        }
        StatusText.Text = "Deleted \"" + a.Title + "\"";
        SetDirty(false);
        await ReloadAssignments(null);
    }

    private async void DownloadFile_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null)
        {
            return;
        }
        ApiResult<DownloadedFile> result;
        using (new BusyScope(Root, StatusText, "Downloading..."))
        {
            result = await QlbtApi.DownloadTeacherAssignmentFile(_editing.Dto.Id, UiHelper.SaveTarget(this));
        }
        ShowSaved(result);
    }

    // ---- Submissions ----

    private async Task ReloadSubmissions()
    {
        AssignmentRow? row = _editing;
        if (row == null)
        {
            ShowSubmissions(null);
            return;
        }
        ApiResult<List<SubmissionDto>> result;
        using (new BusyScope(Root, StatusText, "Loading submissions..."))
        {
            result = await QlbtApi.GetSubmissions(row.Dto.Id);
        }
        if (row != _editing || !UiHelper.Check(this, result))
        {
            return;
        }
        _submissions = (result.Body ?? new List<SubmissionDto>())
            .OrderBy(s => s.StudentUsername)
            .Select(s => new SubmissionRow { Dto = s })
            .ToList();
        ShowSubmissions(row);
    }

    private void ShowSubmissions(AssignmentRow? row)
    {
        int? keepId = _keepSubmissionId ?? (SubmissionsGrid.SelectedItem as SubmissionRow)?.Id;
        _keepSubmissionId = null;
        if (row == null)
        {
            _submissions = new List<SubmissionRow>();
            SubmissionsTitle.Text = "Submissions";
            SubmissionsGrid.ItemsSource = null;
            SubmissionsEmptyText.Text = "Select an assignment to see its submissions.";
            SubmissionsEmptyText.Visibility = Visibility.Visible;
            SubmissionsFooter.Text = "";
            UpdateSubmissionButtons();
            return;
        }

        List<SubmissionRow> visible = _submissions.Where(s => UngradedOnlyBox.IsChecked != true || !s.IsGraded).ToList();
        SubmissionsTitle.Text = "Submissions of " + row.Title;
        SubmissionsGrid.ItemsSource = visible;
        SubmissionsGrid.SelectedItem = visible.FirstOrDefault(s => s.Id == keepId);
        SubmissionsEmptyText.Text = _submissions.Count == 0 ? "No submissions yet." : "Every submission is graded.";
        SubmissionsEmptyText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SubmissionsFooter.Text = _submissions.Count + " submissions · " + _submissions.Count(s => s.IsGraded) + " graded · "
            + _submissions.Count(s => s.IsLate) + " late";
        UpdateSubmissionButtons();
    }

    private void UngradedOnly_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            ShowSubmissions(_editing);
        }
    }

    private void SubmissionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSubmissionButtons();
    }

    private void UpdateSubmissionButtons()
    {
        bool selected = SubmissionsGrid.SelectedItem is SubmissionRow;
        bool closed = _editing != null && _editing.Dto.IsClosed;
        DownloadSubmissionButton.IsEnabled = selected;
        DeleteSubmissionButton.IsEnabled = selected;
        GradeButton.IsEnabled = selected && closed;
        GradeButton.ToolTip = closed
            ? "Grade or re-grade the selected submission"
            : "Close the assignment before grading.";
    }

    private async void DownloadSubmission_Click(object sender, RoutedEventArgs e)
    {
        if (SubmissionsGrid.SelectedItem is not SubmissionRow row)
        {
            return;
        }
        ApiResult<DownloadedFile> result;
        using (new BusyScope(Root, StatusText, "Downloading..."))
        {
            result = await QlbtApi.DownloadSubmission(row.Id, UiHelper.SaveTarget(this));
        }
        ShowSaved(result);
    }

    private void SubmissionsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(SubmissionsGrid, source) is DataGridRow)
        {
            DownloadSubmission_Click(sender, e);
        }
    }

    private async void Grade_Click(object sender, RoutedEventArgs e)
    {
        if (SubmissionsGrid.SelectedItem is not SubmissionRow row || _editing == null)
        {
            return;
        }
        GradeDialog dialog = new GradeDialog(this, row.Dto, _editing.Title);
        dialog.ShowDialog();
        if (!dialog.Changed || Session.User == null)
        {
            return;
        }
        if (dialog.SavedScore != null)
        {
            StatusText.Text = "Graded " + row.StudentUsername + ": " + UiHelper.FormatScore(dialog.SavedScore);
        }
        // Reload the assignments too: the Graded count changes. Keep the same submission selected.
        _keepSubmissionId = row.Id;
        await ReloadAssignments(_editing.Dto.Id);
    }

    private async void DeleteSubmission_Click(object sender, RoutedEventArgs e)
    {
        if (SubmissionsGrid.SelectedItem is not SubmissionRow row || _editing == null)
        {
            return;
        }
        string score = row.IsGraded ? " and its score (" + row.ScoreText + ")" : "";
        if (!UiHelper.Confirm(this, "Delete the submission of " + row.StudentUsername + score + "?\n\n"
            + "The file is deleted and the student can submit again."))
        {
            return;
        }

        ApiResult<object> result;
        using (new BusyScope(Root, StatusText, "Deleting..."))
        {
            result = await QlbtApi.DeleteSubmission(row.Id);
        }
        if (!UiHelper.Check(this, result))
        {
            return;
        }
        StatusText.Text = "Deleted the submission of " + row.StudentUsername;
        await ReloadAssignments(_editing.Dto.Id);
    }

    // ---- Status bar ----

    private void ShowSaved(ApiResult<DownloadedFile> result)
    {
        if (!UiHelper.Check(this, result))
        {
            StatusText.Text = "Download failed.";
            return;
        }
        if (result.Body == null)
        {
            StatusText.Text = "Download cancelled.";
            return;
        }
        _lastSavedPath = result.Body.Path;
        StatusText.Text = "Saved to " + result.Body.Path + " (" + UiHelper.FormatSize(result.Body.Length) + ")";
        OpenFolderLink.Visibility = Visibility.Visible;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSavedPath != null)
        {
            UiHelper.ShowInFolder(_lastSavedPath);
        }
    }
}
