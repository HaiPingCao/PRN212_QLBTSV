using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QLBT.Client.Api;
using QLBT.Shared;

namespace QLBT.Client;

/// <summary>Helpers shared by the client windows: API result handling, file dialogs, messages, formats.</summary>
public static class UiHelper
{
    public const string DateTimeFormat = "dd/MM/yyyy HH:mm";

    /// <summary>
    /// True when the call succeeded. Unauthorized → back to the login window. Any other failure shows the server's
    /// message in <paramref name="errorLine"/> when given, otherwise in a MessageBox.
    /// </summary>
    public static bool Check(DependencyObject owner, ApiResult result, TextBlock? errorLine = null)
    {
        if (result.IsOk)
        {
            return true;
        }
        if (result.Status == ApiStatus.Unauthorized)
        {
            App.SessionExpired();
            return false;
        }
        if (errorLine != null)
        {
            SetMessage(errorLine, result.Message);
        }
        else
        {
            Warn(owner, result.Message);
        }
        return false;
    }

    /// <summary>Shows the text, or collapses the TextBlock when the text is null or empty.</summary>
    public static void SetMessage(TextBlock target, string? text)
    {
        target.Text = text ?? "";
        target.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    public static void Warn(DependencyObject owner, string message)
    {
        MessageBox.Show(Window.GetWindow(owner), message, "QLBT", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public static bool Confirm(DependencyObject owner, string message)
    {
        return MessageBox.Show(Window.GetWindow(owner), message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public static bool ConfirmDiscard(DependencyObject owner, bool dirty)
    {
        return !dirty || Confirm(owner, "Discard unsaved changes?");
    }

    /// <summary>Open dialog for an upload (any file type; one file, zip several).</summary>
    public static string? PickFile(DependencyObject owner, string title)
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Title = title + " (1 file; zip several files)";
        dialog.Filter = "All files (*.*)|*.*|Archives (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z";
        return dialog.ShowDialog(Window.GetWindow(owner)) == true ? dialog.FileName : null;
    }

    /// <summary>Returns a target chooser for ApiClient.Download: a Save dialog pre-filled with the original name.</summary>
    public static Func<string, string?> SaveTarget(DependencyObject owner)
    {
        return fileName =>
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "Save " + fileName;
            dialog.FileName = fileName;
            string extension = Path.GetExtension(fileName);
            dialog.Filter = string.IsNullOrEmpty(extension)
                ? "All files (*.*)|*.*"
                : extension.TrimStart('.').ToUpperInvariant() + " file (*" + extension + ")|*" + extension + "|All files (*.*)|*.*";
            return dialog.ShowDialog(Window.GetWindow(owner)) == true ? dialog.FileName : null;
        };
    }

    /// <summary>Opens Explorer with the file selected.</summary>
    public static void ShowInFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Opening the folder is a convenience only.
        }
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }
        if (bytes < 1024 * 1024)
        {
            return (bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
        }
        return (bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
    }

    public static string FileSizeOf(string path)
    {
        try
        {
            return FormatSize(new FileInfo(path).Length);
        }
        catch (IOException)
        {
            return "";
        }
    }

    public static string FormatScore(decimal? score)
    {
        return score == null ? "" : score.Value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}

/// <summary>Disables the window content while a request runs and shows a status text; Dispose restores it.</summary>
public sealed class BusyScope : IDisposable
{
    private readonly UIElement _content;
    private readonly TextBlock _status;

    public BusyScope(UIElement content, TextBlock status, string text)
    {
        _content = content;
        _status = status;
        _content.IsEnabled = false;
        _status.Text = text;
    }

    public void Dispose()
    {
        _content.IsEnabled = true;
        if (_status.Text.EndsWith("...", StringComparison.Ordinal))
        {
            _status.Text = "Ready";
        }
    }
}
