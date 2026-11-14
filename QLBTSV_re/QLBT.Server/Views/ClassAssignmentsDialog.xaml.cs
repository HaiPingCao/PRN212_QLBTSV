using System.Windows;
using System.Windows.Controls;
using QLBT.Server.Admin;

namespace QLBT.Server.Views;

/// <summary>
/// Read-only view of a class's assignments and the submissions of the selected one.
/// The admin only looks; teachers manage assignments and grades from the client.
/// </summary>
public sealed partial class ClassAssignmentsDialog : Window
{
    private readonly ClassRow _class;
    private readonly ClassAdminService _service;

    public ClassAssignmentsDialog(ClassRow targetClass, ClassAdminService service)
    {
        InitializeComponent();
        _class = targetClass;
        _service = service;
        Title = "Assignments of " + targetClass.Name + " · " + targetClass.CourseCode;
        TitleText.Text = "Assignments of " + targetClass.Name + " · " + targetClass.CourseCode + " (" + targetClass.SemesterName + ")"
            + " · Teacher: " + targetClass.TeacherDisplay + " · " + targetClass.ActiveCount + " active student(s)";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        List<AssignmentRow>? assignments = await ViewHelper.Run(Root, () => _service.ListAssignments(_class.Id));
        if (assignments == null)
        {
            Close();
            return;
        }

        AssignmentsGrid.ItemsSource = assignments;
        NoAssignmentsText.Visibility = assignments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (assignments.Count > 0)
        {
            AssignmentsGrid.SelectedIndex = 0; // AssignmentsGrid_SelectionChanged loads its submissions
        }
    }

    private async void AssignmentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AssignmentRow? selected = AssignmentsGrid.SelectedItem as AssignmentRow;
        bool hasFile = selected?.FileName != null;
        OpenAssignmentButton.IsEnabled = hasFile;
        ShowAssignmentButton.IsEnabled = hasFile;
        AssignmentPathBox.Text = selected == null ? "" : hasFile ? _service.AssignmentFilePath(_class, selected.Id) : "No file attached.";
        UpdateSubmissionFile();

        if (selected is not AssignmentRow assignment)
        {
            SubmissionsGrid.ItemsSource = null;
            SubmissionsTitle.Text = "Submissions (select an assignment)";
            return;
        }

        List<SubmissionRow>? submissions = await ViewHelper.Run(Root, () => _service.ListSubmissions(assignment.Id));
        SubmissionsGrid.ItemsSource = submissions;
        SubmissionsTitle.Text = "Submissions of " + assignment.Title + " (" + (submissions?.Count ?? 0) + " of "
            + _class.ActiveCount + " active student(s), " + assignment.GradedCount + " graded)";
    }

    private void SubmissionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSubmissionFile();
    }

    private void UpdateSubmissionFile()
    {
        SubmissionRow? selected = SubmissionsGrid.SelectedItem as SubmissionRow;
        OpenSubmissionButton.IsEnabled = selected != null;
        ShowSubmissionButton.IsEnabled = selected != null;
        SubmissionPathBox.Text = selected == null ? "" : _service.SubmissionFilePath(_class, selected);
    }

    private void OpenAssignmentFile_Click(object sender, RoutedEventArgs e)
    {
        if (AssignmentsGrid.SelectedItem is AssignmentRow assignment && assignment.FileName != null)
        {
            ViewHelper.OpenStoredFile(this, _service.AssignmentFilePath(_class, assignment.Id), assignment.FileName);
        }
    }

    private void ShowAssignmentFile_Click(object sender, RoutedEventArgs e)
    {
        if (AssignmentsGrid.SelectedItem is AssignmentRow assignment && assignment.FileName != null)
        {
            ViewHelper.ShowInFolder(this, _service.AssignmentFilePath(_class, assignment.Id));
        }
    }

    private void OpenSubmissionFile_Click(object sender, RoutedEventArgs e)
    {
        if (SubmissionsGrid.SelectedItem is SubmissionRow submission)
        {
            ViewHelper.OpenStoredFile(this, _service.SubmissionFilePath(_class, submission), submission.FileName);
        }
    }

    private void ShowSubmissionFile_Click(object sender, RoutedEventArgs e)
    {
        if (SubmissionsGrid.SelectedItem is SubmissionRow submission)
        {
            ViewHelper.ShowInFolder(this, _service.SubmissionFilePath(_class, submission));
        }
    }
}
