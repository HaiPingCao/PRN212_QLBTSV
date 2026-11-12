using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QLBT.Client.Api;
using QLBT.Shared;

namespace QLBT.Client.Views.Student;

/// <summary>
/// Student UI: assignments (tree by class, or flat by due date) on the left, the selected assignment and
/// "My submission" on the right. The server decides everything; the buttons only mirror its rules.
/// </summary>
public sealed partial class StudentWindow : Window
{
    private List<StudentAssignmentItem> _items = new List<StudentAssignmentItem>();
    private StudentAssignmentItem? _current;
    private MySubmissionDto? _submission;
    private string? _lastSavedPath;
    private int _detailRequest; // ignores answers for an assignment that is no longer selected

    public StudentWindow()
    {
        InitializeComponent();
        UserText.Text = Session.UserDisplay;
        Title = "QLBT – " + Session.UserDisplay;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await Reload();
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
            await Reload();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await Reload();
    }

    private void Profile_Click(object sender, RoutedEventArgs e)
    {
        new ProfileDialog(this).ShowDialog();
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        await App.Logout(this);
    }

    // ---- List ----

    private async Task Reload()
    {
        int? keepId = _current?.Id;
        ApiResult<List<StudentAssignmentDto>> result;
        using (new BusyScope(Root, StatusText, "Loading..."))
        {
            result = await QlbtApi.GetMyAssignments();
        }
        if (!UiHelper.Check(this, result))
        {
            return;
        }

        _items = (result.Body ?? new List<StudentAssignmentDto>())
            .Select(a => new StudentAssignmentItem { Dto = a })
            .ToList();
        StatusText.Text = "Loaded " + _items.Count + " assignments at " + DateTime.Now.ToString("HH:mm");
        ShowList(keepId);
    }

    private void View_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            ShowList(_current?.Id);
        }
    }

    /// <summary>Rebuilds the tree or the due-date grid from _items and re-selects <paramref name="selectId"/>.</summary>
    private void ShowList(int? selectId)
    {
        List<StudentAssignmentItem> visible = _items
            .Where(i => HideClosedBox.IsChecked != true || !i.Dto.IsClosed)
            .ToList();
        foreach (StudentAssignmentItem item in _items)
        {
            item.IsSelected = false;
        }

        bool byClass = ByClassRadio.IsChecked == true;
        AssignmentTree.Visibility = byClass ? Visibility.Visible : Visibility.Collapsed;
        DueGrid.Visibility = byClass ? Visibility.Collapsed : Visibility.Visible;
        StudentAssignmentItem? selected = visible.FirstOrDefault(i => i.Id == selectId);

        if (byClass)
        {
            List<StudentClassGroup> groups = visible
                .GroupBy(i => i.Dto.ClassId)
                .Select(g => g.First().Dto)
                .OrderBy(d => d.CourseCode).ThenBy(d => d.ClassName)
                .Select(d => new StudentClassGroup
                {
                    Header = d.CourseCode + " · " + d.ClassName + (d.ClassSuspended ? " (suspended)" : ""),
                    ToolTip = d.CourseCode + " – " + d.CourseName,
                    IsSuspended = d.ClassSuspended,
                    Items = visible.Where(i => i.Dto.ClassId == d.ClassId).OrderBy(i => i.DueDate).ToList()
                })
                .ToList();
            if (selected != null)
            {
                selected.IsSelected = true; // the TreeViewItem binds IsSelected two-way
            }
            AssignmentTree.ItemsSource = groups;
        }
        else
        {
            DueGrid.ItemsSource = visible.OrderBy(i => i.DueDate).ToList();
            DueGrid.SelectedItem = selected;
            if (selected != null)
            {
                DueGrid.ScrollIntoView(selected);
            }
        }

        ListEmptyText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        int open = visible.Count(i => !i.Dto.IsClosed && !i.Dto.ClassSuspended);
        CountText.Text = visible.Count + " assignments · " + open + " open";
        _ = ShowDetail(selected);
    }

    private async void AssignmentTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        await ShowDetail(e.NewValue as StudentAssignmentItem);
    }

    private async void DueGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ShowDetail(DueGrid.SelectedItem as StudentAssignmentItem);
    }

    // ---- Detail ----

    private async Task ShowDetail(StudentAssignmentItem? item)
    {
        if (item == _current && item != null && DetailPanel.Visibility == Visibility.Visible && _submission != null)
        {
            return;
        }
        _current = item;
        _submission = null;
        int request = ++_detailRequest;

        if (item == null)
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            DetailEmptyText.Visibility = Visibility.Visible;
            return;
        }
        DetailPanel.Visibility = Visibility.Visible;
        DetailEmptyText.Visibility = Visibility.Collapsed;

        StudentAssignmentDto a = item.Dto;
        AssignmentTitle.Text = a.Title;
        ClassText.Text = a.ClassName + " · " + a.CourseCode + " – " + a.CourseName;
        DueText.Text = a.DueDate.ToString(UiHelper.DateTimeFormat);
        StatusValueText.Text = item.StatusText;
        StatusValueText.Foreground = (System.Windows.Media.Brush)FindResource(a.IsClosed || a.ClassSuspended ? "StatusMutedBrush" : "StatusOkBrush");
        SetReadOnlyText(DescriptionBox, a.Description, "No description.");

        DownloadAssignmentButton.IsEnabled = a.HasFile && !a.ClassSuspended;
        if (a.ClassSuspended)
        {
            UiHelper.SetMessage(AssignmentInfo, "ℹ You are suspended in this class: you cannot download the assignment or submit. "
                + "You can still view your score, comment and old submission.");
        }
        else
        {
            UiHelper.SetMessage(AssignmentInfo, a.HasFile ? null : "ℹ This assignment has no file.");
        }

        ShowSubmission(null, "Loading...");
        ApiResult<MySubmissionDto> result = await QlbtApi.GetMySubmission(a.Id);
        if (request != _detailRequest)
        {
            return;
        }
        if (result.IsOk)
        {
            _submission = result.Body;
            ShowSubmission(result.Body, null);
        }
        else if (result.Status == ApiStatus.NotFound)
        {
            ShowSubmission(null, "Not submitted yet.");
        }
        else if (UiHelper.Check(this, result))
        {
            ShowSubmission(null, null);
        }
        else
        {
            ShowSubmission(null, result.Message);
        }
    }

    private void ShowSubmission(MySubmissionDto? submission, string? state)
    {
        StudentAssignmentDto? a = _current?.Dto;
        SubmissionStateText.Text = state ?? "";
        SubmissionStateText.Visibility = string.IsNullOrEmpty(state) ? Visibility.Collapsed : Visibility.Visible;
        SubmissionGrid.Visibility = submission == null ? Visibility.Collapsed : Visibility.Visible;

        if (submission != null)
        {
            SubmissionFileText.Text = submission.FileName;
            SubmittedText.Text = submission.SubmittedAt.ToString(UiHelper.DateTimeFormat)
                + (submission.SubmitCount > 1 ? " (" + submission.SubmitCount + " times)" : "");
            LateText.Text = submission.IsLate ? "Yes" : "No";
            LateText.Foreground = (System.Windows.Media.Brush)FindResource(submission.IsLate ? "StatusWarnBrush" : "StatusOkBrush");
            ScoreText.Text = submission.Score == null ? "Not graded" : UiHelper.FormatScore(submission.Score) + " / 10";
        }
        SetReadOnlyText(CommentBox, submission?.Comment, "No comment yet.");

        DownloadSubmissionButton.IsEnabled = submission != null;
        SubmitButton.Content = submission == null ? "Submit…" : "Resubmit…";

        string? blocked = null;
        if (a == null || state == "Loading...")
        {
            blocked = "";
        }
        else if (a.ClassSuspended)
        {
            blocked = "ℹ Suspended in this class: submitting is not allowed.";
        }
        else if (a.IsClosed)
        {
            blocked = "ℹ The assignment is closed.";
        }
        else if (submission?.Score != null)
        {
            blocked = "ℹ Graded: you can no longer resubmit.";
        }
        SubmitButton.IsEnabled = blocked == null;
        UiHelper.SetMessage(SubmitInfo, blocked == null
            ? (submission == null ? "ℹ One file per assignment; zip several files." : "ℹ Resubmitting replaces your previous file.")
            : blocked);
    }

    private void SetReadOnlyText(TextBox box, string? text, string emptyText)
    {
        bool empty = string.IsNullOrWhiteSpace(text);
        box.Text = empty ? emptyText : text;
        box.Foreground = empty ? (System.Windows.Media.Brush)FindResource("StatusMutedBrush") : SystemColors.ControlTextBrush;
        box.FontStyle = empty ? FontStyles.Italic : FontStyles.Normal;
    }

    // ---- Actions ----

    private async void DownloadAssignment_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null)
        {
            return;
        }
        ApiResult<DownloadedFile> result;
        using (new BusyScope(Root, StatusText, "Downloading..."))
        {
            result = await QlbtApi.DownloadAssignment(_current.Id, UiHelper.SaveTarget(this));
        }
        ShowSaved(result);
    }

    private async void DownloadSubmission_Click(object sender, RoutedEventArgs e)
    {
        if (_submission == null)
        {
            return;
        }
        ApiResult<DownloadedFile> result;
        using (new BusyScope(Root, StatusText, "Downloading..."))
        {
            result = await QlbtApi.DownloadSubmission(_submission.Id, UiHelper.SaveTarget(this));
        }
        ShowSaved(result);
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null)
        {
            return;
        }
        StudentAssignmentItem item = _current;
        string? file = UiHelper.PickFile(this, "Submit " + item.Title);
        if (file == null)
        {
            return;
        }
        if (_submission != null && !UiHelper.Confirm(this, "Replace your submission " + _submission.FileName + " with " + Path.GetFileName(file) + "?"))
        {
            return;
        }

        ApiResult<SubmitResult> result;
        using (new BusyScope(Root, StatusText, "Uploading " + Path.GetFileName(file) + " (" + UiHelper.FileSizeOf(file) + ")..."))
        {
            result = await QlbtApi.SubmitAssignment(item.Id, file);
        }
        if (!UiHelper.Check(this, result))
        {
            StatusText.Text = "Submit failed.";
            return;
        }

        bool late = result.Body?.IsLate == true;
        StatusText.Text = "Submitted " + Path.GetFileName(file) + (late ? " (late)" : "") + " at " + DateTime.Now.ToString("HH:mm");
        OpenFolderLink.Visibility = Visibility.Collapsed;
        _current = null; // force the detail to reload
        await ShowDetail(item);
    }

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
