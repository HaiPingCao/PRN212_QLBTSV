using System.Windows;
using System.Windows.Controls;
using QLBT.Server.Admin;

namespace QLBT.Server.Views;

/// <summary>Small helpers shared by the admin views: messages, confirmations and DB errors.</summary>
public static class ViewHelper
{
    /// <summary>Shows the text, or collapses the TextBlock when the text is null or empty.</summary>
    public static void SetMessage(TextBlock target, string? text)
    {
        target.Text = text ?? "";
        target.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    public static bool Confirm(DependencyObject owner, string message)
    {
        return MessageBox.Show(Window.GetWindow(owner), message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            == MessageBoxResult.Yes;
    }

    public static void Warn(DependencyObject owner, string message)
    {
        MessageBox.Show(Window.GetWindow(owner), message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static bool _errorOpen;
    private static string? _lastError;
    private static DateTime _lastErrorClosedAt;

    /// <summary>
    /// Unexpected failure (usually the database is unreachable): tell the admin and log it.
    /// Several views load at once, so the same error is shown only once: while a dialog is open,
    /// or for a few seconds after it closes, repeats are only logged.
    /// </summary>
    public static void ShowError(DependencyObject owner, Exception ex)
    {
        Exception root = ex.GetBaseException();
        AdminLog.Write("Error: " + root.Message);
        bool repeat = root.Message == _lastError && DateTime.Now - _lastErrorClosedAt < TimeSpan.FromSeconds(3);
        if (_errorOpen || repeat)
        {
            return;
        }

        _errorOpen = true;
        try
        {
            MessageBox.Show(Window.GetWindow(owner), "The operation could not be completed. Please verify that the database server is available.\n\nDetails: " + root.Message,
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _errorOpen = false;
            _lastError = root.Message;
            _lastErrorClosedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Runs a database call with <paramref name="busy"/> disabled. Returns the result,
    /// or null after showing the error when the call throws.
    /// </summary>
    public static async Task<T?> Run<T>(FrameworkElement busy, Func<Task<T>> work) where T : class
    {
        busy.IsEnabled = false;
        try
        {
            return await work();
        }
        catch (Exception ex)
        {
            ShowError(busy, ex);
            return null;
        }
        finally
        {
            busy.IsEnabled = true;
        }
    }

    /// <summary>Same as <see cref="Run{T}"/> for work without a single result. Returns false when it failed.</summary>
    public static async Task<bool> TryRun(FrameworkElement busy, Func<Task> work)
    {
        return await Run(busy, async () =>
        {
            await work();
            return "";
        }) != null;
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
