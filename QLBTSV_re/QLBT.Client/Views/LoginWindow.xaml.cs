using System.Windows;
using QLBT.Client.Api;
using QLBT.Shared;

namespace QLBT.Client.Views;

/// <summary>Login; the server address comes from appsettings.json (read-only here).</summary>
public sealed partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        ServerText.Text = "Server: " + Session.Client.BaseAddress;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UsernameBox.Focus();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        string username = UsernameBox.Text.Trim();
        if (username.Length == 0 || PasswordBox.Password.Length == 0)
        {
            UiHelper.SetMessage(ErrorText, "Enter your username and password.");
            return;
        }

        UiHelper.SetMessage(ErrorText, null);
        Root.IsEnabled = false;
        LoginButton.Content = "Logging in...";
        ApiResult<LoginResponse> result;
        try
        {
            result = await Session.Login(username, PasswordBox.Password);
        }
        finally
        {
            Root.IsEnabled = true;
            LoginButton.Content = "Login";
        }

        if (!result.IsOk || Session.User == null)
        {
            UiHelper.SetMessage(ErrorText, result.Message);
            PasswordBox.Clear();
            PasswordBox.Focus();
            return;
        }
        App.ShowMain(this);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        App.TopLevelClosed();
    }
}
