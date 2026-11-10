using System.Windows;
using System.Windows.Controls;
using QLBT.Server.Admin;

namespace QLBT.Server.Views;

/// <summary>Small helpers shared by the admin views: messages, confirmations and DB errors.</summary>
public static class ViewHelper
{
    public const string DateFormat = "dd/MM/yyyy";

    /// <summary>Shows the text, or collapses the TextBlock when the text is null or empty.</summary>
    public static void SetMessage(TextBlock target, string? text)
    {
        target.Text = text ?? "";
        target.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    public static bool Confirm(DependencyObject owner, string message)
    {
        return MessageBox.Show(Window.GetWindow(owner), message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            == MessageBoxResult.Yes;
    }

    public static void Warn(DependencyObject owner, string message)
    {
        MessageBox.Show(Window.GetWindow(owner), message, "QLBT Server", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>Unexpected failure (usually the database is unreachable): tell the admin and log it.</summary>
    public static void ShowError(DependencyObject owner, Exception ex)
    {
        Exception root = ex.GetBaseException();
        AdminLog.Write("Error: " + root.Message);
        MessageBox.Show(Window.GetWindow(owner), "Cannot complete the operation (is the database reachable?):\n\n" + root.Message,
            "QLBT Server", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>"Discard changes?" when the editor has unsaved edits. True = go ahead.</summary>
    public static bool ConfirmDiscard(DependencyObject owner, bool dirty)
    {
        return !dirty || Confirm(owner, "Discard unsaved changes?");
    }

    public static DateOnly? ToDateOnly(DateTime? value)
    {
        return value == null ? null : DateOnly.FromDateTime(value.Value);
    }

    public static DateTime ToDateTime(DateOnly value)
    {
        return value.ToDateTime(TimeOnly.MinValue);
    }
}
