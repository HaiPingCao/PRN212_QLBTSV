using System.Globalization;
using System.Windows;
using System.Windows.Input;
using QLBT.Client.Api;
using QLBT.Shared;

namespace QLBT.Client.Views.Teacher;

/// <summary>
/// Grade or re-grade one submission (PUT grade). Stays open on a rule error (e.g. the assignment was reopened)
/// so the typed comment is not lost. <see cref="Changed"/> tells the owner to reload.
/// </summary>
public sealed partial class GradeDialog : Window
{
    private readonly SubmissionDto _submission;

    /// <summary>True when the owner should reload: graded, or the submission no longer exists.</summary>
    public bool Changed { get; private set; }

    public decimal? SavedScore { get; private set; }

    public GradeDialog(Window owner, SubmissionDto submission, string assignmentTitle)
    {
        InitializeComponent();
        Owner = owner;
        _submission = submission;
        Title = "Grade " + submission.StudentUsername + " – " + assignmentTitle;
        StudentText.Text = submission.StudentUsername + " · " + submission.StudentName;
        SubmittedText.Text = submission.SubmittedAt.ToString(UiHelper.DateTimeFormat)
            + (submission.SubmitCount > 1 ? " (" + submission.SubmitCount + " times)" : "")
            + (submission.IsLate ? " · Late" : "");
        FileText.Text = submission.FileName;
        ScoreBox.Text = UiHelper.FormatScore(submission.Score);
        CommentBox.Text = submission.Comment ?? "";
        StatusText.Text = submission.Score == null ? "Not graded yet" : "Re-grading overwrites the score and comment.";
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ScoreBox.Focus();
        ScoreBox.SelectAll();
        // Enter in the score box saves; Enter in the comment adds a line.
        ScoreBox.KeyDown += (s, args) =>
        {
            if (args.Key == Key.Enter)
            {
                Save_Click(SaveButton, new RoutedEventArgs());
            }
        };
    }

    /// <summary>0..10 with at most 2 decimals; accepts "8.5" and "8,5".</summary>
    private static bool TryParseScore(string text, out decimal score)
    {
        string normalized = text.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out score))
        {
            return false;
        }
        return score >= 0 && score <= 10 && decimal.Round(score, 2) == score;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ScoreBox.Text.Trim().Length == 0)
        {
            UiHelper.SetMessage(ErrorText, "Enter a score.");
            ScoreBox.Focus();
            return;
        }
        if (!TryParseScore(ScoreBox.Text, out decimal score))
        {
            UiHelper.SetMessage(ErrorText, "Score must be between 0 and 10 with at most 2 decimals.");
            ScoreBox.Focus();
            return;
        }
        UiHelper.SetMessage(ErrorText, null);

        GradeRequest request = new GradeRequest
        {
            Score = score,
            Comment = string.IsNullOrWhiteSpace(CommentBox.Text) ? null : CommentBox.Text.Trim()
        };
        ApiResult<object> result;
        Root.IsEnabled = false;
        StatusText.Text = "Saving...";
        try
        {
            result = await QlbtApi.GradeSubmission(_submission.Id, request);
        }
        finally
        {
            Root.IsEnabled = true;
            StatusText.Text = "";
        }

        if (result.IsOk)
        {
            Changed = true;
            SavedScore = score;
            DialogResult = true;
            return;
        }
        if (result.Status == ApiStatus.NotFound)
        {
            Changed = true;
            UiHelper.Warn(this, result.Message);
            DialogResult = false;
            return;
        }
        UiHelper.Check(this, result, ErrorText);
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        Root.IsEnabled = false;
        StatusText.Text = "Downloading...";
        ApiResult<DownloadedFile> result;
        try
        {
            result = await QlbtApi.DownloadSubmission(_submission.Id, UiHelper.SaveTarget(this));
        }
        finally
        {
            Root.IsEnabled = true;
            StatusText.Text = "";
        }
        if (UiHelper.Check(this, result) && result.Body != null)
        {
            StatusText.Text = "Saved to " + result.Body.Path;
        }
    }
}
