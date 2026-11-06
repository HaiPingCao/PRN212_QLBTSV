using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Svr3.Server;

public partial class MainWindow : Window
{
    private const int MaxLines = 1000;

    private readonly ServerRunner _runner;
    private readonly ObservableCollection<string> _lines = new ObservableCollection<string>();
    private ServerSettings _settings;
    private bool _busy;

    public MainWindow(ServerRunner runner, UiLoggerProvider log)
    {
        InitializeComponent();
        _runner = runner;
        LogList.ItemsSource = _lines;
        log.LineWritten += AppendLog;

        foreach (string address in GetLocalAddresses())
        {
            AddressBox.Items.Add(address);
        }

        _settings = ServerSettings.Load();
        AddressBox.Text = _settings.BindAddress;
        PortBox.Text = _settings.Port.ToString();
        StorageBox.Text = _settings.StoragePath;
        AutoStartBox.IsChecked = _settings.AutoStart;

        ShowState(null);
    }

    /// <summary>Called once when the app opens: starts the server if "Start server when the app opens" is on.</summary>
    public async Task StartIfAutoStart()
    {
        if (_settings.AutoStart)
        {
            await StartServer();
        }
        else
        {
            Log("Auto start is off. Press Start to run the server.");
        }
    }

    /// <summary>Saves the fields and starts the server.</summary>
    public async Task StartServer()
    {
        ServerSettings? settings = ReadFields();
        if (settings == null)
        {
            return;
        }
        settings.Save();
        _settings = settings;

        SetBusy(true, "Starting...");
        string? error = null;
        try
        {
            await _runner.Start(settings);
            Log("Server started on " + settings.BindAddress + ":" + settings.Port + ", files in " + settings.StoragePath);
        }
        catch (Exception ex) // port in use, address not on this PC, bad appsettings.json...
        {
            error = "Failed to start: " + ex.Message;
            Log(error);
        }
        SetBusy(false, null);
        ShowState(error);
    }

    /// <summary>Safe to call from any thread.</summary>
    public void AppendLog(string line)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _lines.Add(line);
            if (_lines.Count > MaxLines)
            {
                _lines.RemoveAt(0);
            }
            LogList.ScrollIntoView(line);
        });
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        await StartServer();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Stopping...");
        await _runner.Stop();
        Log("Server stopped.");
        SetBusy(false, null);
        ShowState(null);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ServerSettings? settings = ReadFields();
        if (settings == null)
        {
            return;
        }
        settings.Save();
        _settings = settings;
        Log("Settings saved.");
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new OpenFolderDialog();
        dialog.Title = "Folder for assignment and submission files";
        if (Directory.Exists(StorageBox.Text))
        {
            dialog.InitialDirectory = StorageBox.Text;
        }
        if (dialog.ShowDialog(this) == true)
        {
            StorageBox.Text = dialog.FolderName;
        }
    }

    /// <summary>Saved immediately, whether or not the server is running. Takes effect the next time the app opens.</summary>
    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        bool autoStart = AutoStartBox.IsChecked == true;
        if (_settings == null || _settings.AutoStart == autoStart)
        {
            return; // fired by the constructor loading the saved value
        }
        _settings.AutoStart = autoStart;
        _settings.Save();
        Log(_settings.AutoStart ? "Server will start when the app opens." : "Auto start turned off.");
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
    }

    /// <returns>Validated settings, or null after telling the user what is wrong.</returns>
    private ServerSettings? ReadFields()
    {
        string addressText = AddressBox.Text.Trim();
        if (!IPAddress.TryParse(addressText, out IPAddress? address))
        {
            return Invalid("IP address is not valid: " + addressText);
        }

        if (!int.TryParse(PortBox.Text.Trim(), out int port) || port < 1 || port > 65535)
        {
            return Invalid("Port must be a number from 1 to 65535.");
        }

        string storage = StorageBox.Text.Trim();
        if (storage.Length == 0)
        {
            return Invalid("File save path is required.");
        }
        try
        {
            storage = Path.GetFullPath(storage, AppContext.BaseDirectory);
            Directory.CreateDirectory(storage);
        }
        catch (Exception ex)
        {
            return Invalid("Cannot use file save path: " + ex.Message);
        }

        StorageBox.Text = storage;
        return new ServerSettings
        {
            BindAddress = address.ToString(),
            Port = port,
            StoragePath = storage,
            AutoStart = AutoStartBox.IsChecked == true
        };
    }

    private ServerSettings? Invalid(string message)
    {
        MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        return null;
    }

    private void SetBusy(bool busy, string? status)
    {
        _busy = busy;
        if (status != null)
        {
            StatusText.Text = status;
            StatusText.Foreground = Brushes.DarkGoldenrod;
        }
        UpdateButtons();
    }

    private void ShowState(string? error)
    {
        if (_runner.IsRunning)
        {
            StatusText.Text = "Running on http://" + _settings.BindAddress + ":" + _settings.Port;
            StatusText.Foreground = Brushes.Green;
        }
        else if (error != null)
        {
            StatusText.Text = error;
            StatusText.Foreground = Brushes.Red;
        }
        else
        {
            StatusText.Text = "Stopped";
            StatusText.Foreground = Brushes.Gray;
        }
        UpdateButtons();
    }

    /// <summary>Address, port and path are editable only while stopped; they apply on the next Start.</summary>
    private void UpdateButtons()
    {
        bool running = _runner.IsRunning;
        bool editable = !running && !_busy;
        AddressBox.IsEnabled = editable;
        PortBox.IsEnabled = editable;
        StorageBox.IsEnabled = editable;
        BrowseButton.IsEnabled = editable;
        SaveButton.IsEnabled = editable;
        StartButton.IsEnabled = editable;
        StopButton.IsEnabled = running && !_busy;
    }

    private void Log(string message)
    {
        AppendLog(DateTime.Now.ToString("HH:mm:ss") + " [Information] Server: " + message);
    }

    /// <summary>0.0.0.0 (all interfaces), 127.0.0.1, then this PC's IPv4 addresses.</summary>
    private static List<string> GetLocalAddresses()
    {
        List<string> addresses = new List<string> { "0.0.0.0", "127.0.0.1" };
        try
        {
            foreach (IPAddress ip in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !addresses.Contains(ip.ToString()))
                {
                    addresses.Add(ip.ToString());
                }
            }
        }
        catch (SocketException)
        {
        }
        return addresses;
    }
}
