using System.Windows;
using QLBT.Client.Api;
using QLBT.Shared;

namespace QLBT.Client.Views;

/// <summary>Read-only profile (GetProfile), fresh from the server.</summary>
public sealed partial class ProfileDialog : Window
{
    public ProfileDialog(Window owner)
    {
        InitializeComponent();
        Owner = owner;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApiResult<UserDto> result = await QlbtApi.GetProfile();
        if (!UiHelper.Check(this, result) || result.Body == null)
        {
            StatusText.Text = result.IsOk ? "" : result.Message;
            return;
        }

        UserDto user = result.Body;
        UsernameText.Text = user.Username;
        FullNameText.Text = user.FullName;
        EmailText.Text = user.Email;
        RoleText.Text = user.Role.ToString();
        StatusText.Text = "";
    }
}
