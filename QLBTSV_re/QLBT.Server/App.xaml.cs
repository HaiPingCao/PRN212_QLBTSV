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

            UiLoggerProvider log = new UiLoggerProvider();
            _runner = new ServerRunner(log);

            MainWindow window = new MainWindow(_runner, log);
            window.Show();
            await window.StartIfAutoStart();
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
