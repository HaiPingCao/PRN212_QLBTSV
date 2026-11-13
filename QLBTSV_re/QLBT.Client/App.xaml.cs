using System.Windows;
using QLBT.Client.Api;
using QLBT.Client.Views;
using QLBT.Client.Views.Student;
using QLBT.Client.Views.Teacher;
using QLBT.Shared;

namespace QLBT.Client;

/// <summary>
/// Navigation: LoginWindow → TeacherWindow / StudentWindow (by role) → Logout or session expiry → LoginWindow.
/// ShutdownMode is OnExplicitShutdown so swapping windows never ends the app; closing a window by the user does.
/// </summary>
public partial class App : Application
{
    private static bool _switching;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        ShowLogin();
    }

    public static void ShowLogin()
    {
        LoginWindow login = new LoginWindow();
        Current.MainWindow = login;
        login.Show();
    }

    /// <summary>Opens the main window of the logged-in user's role and closes the login window.</summary>
    public static void ShowMain(Window login)
    {
        Window main = Session.User != null && Session.User.Role == UserRole.Teacher
            ? new TeacherWindow()
            : new StudentWindow();
        Current.MainWindow = main;
        main.Show();
        CloseWithoutExit(login);
    }

    public static async Task Logout(Window main)
    {
        await Session.Logout();
        ShowLogin();
        CloseWithoutExit(main);
    }

    /// <summary>Unauthorized from the server: the session is gone (server restarted or stopped). Back to login.</summary>
    public static void SessionExpired()
    {
        if (_switching || Session.User == null)
        {
            return;
        }
        Session.Expire();
        Window? owner = Current.MainWindow;
        MessageBox.Show(owner, "Your session has expired. Please log in again.", "Session Expired", MessageBoxButton.OK, MessageBoxImage.Information);

        List<Window> open = Current.Windows.Cast<Window>().ToList();
        ShowLogin();
        foreach (Window window in open)
        {
            CloseWithoutExit(window);
        }
    }

    /// <summary>Called from each top-level window's Closed event: a window closed by the user ends the app.</summary>
    public static void TopLevelClosed()
    {
        if (!_switching)
        {
            Current.Shutdown();
        }
    }

    private static void CloseWithoutExit(Window window)
    {
        _switching = true;
        try
        {
            window.Close();
        }
        finally
        {
            _switching = false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Session.Client.SessionId != null)
        {
            // Best effort: free the server-side session. Off the UI thread to avoid a dispatcher deadlock.
            Task.Run(Session.Logout).Wait(TimeSpan.FromSeconds(2));
        }
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(MainWindow, "An unexpected error has occurred:\n\n" + e.Exception.GetBaseException().Message, "Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
