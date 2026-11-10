using QLBT.Server.Asp;
using System.Windows;

namespace QLBT.Server
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServerRunner? _runner;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            UiLoggerProvider log = new UiLoggerProvider();
            _runner = new ServerRunner(log);

            MainWindow window = new MainWindow(_runner, log);
            window.Show();
            await window.StartIfAutoStart();
        }

        /// <summary>Safety net for UI bugs: show and log the error instead of closing the server (and its HTTP API).</summary>
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            Exception root = e.Exception.GetBaseException();
            Admin.AdminLog.Write("Unexpected error: " + e.Exception);
            MessageBox.Show(MainWindow, "Unexpected error:\n\n" + root.Message + "\n\nDetails are in the Server log tab.",
                "QLBT Server", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_runner != null)
            {
                // Off the UI thread so Kestrel's shutdown can't deadlock on the WPF dispatcher.
                Task.Run(_runner.Stop).GetAwaiter().GetResult();
            }
            base.OnExit(e);
        }
    }

}
