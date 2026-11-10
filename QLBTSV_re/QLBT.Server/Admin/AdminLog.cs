namespace QLBT.Server.Admin;

/// <summary>Admin actions for the "Server log" tab. MainWindow subscribes; views never reference MainWindow.</summary>
public static class AdminLog
{
    public static event Action<string>? LineWritten;

    public static void Write(string message)
    {
        LineWritten?.Invoke(DateTime.Now.ToString("HH:mm:ss") + " [Admin] " + message);
    }
}
