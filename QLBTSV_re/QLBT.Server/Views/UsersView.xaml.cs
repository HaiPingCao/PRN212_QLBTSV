using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QLBT.Server.Admin;
using QLBT.Server.Domain;
using QLBT.Shared;

namespace QLBT.Server.Views;

/// <summary>
/// Users tab: editor on top, list below. Selecting a row edits it; "+ New" clears the editor to add a user.
/// Username and role are locked after creation; a filled password field resets the password.
/// </summary>
public sealed partial class UsersView : UserControl
{
    private readonly UserAdminService _service = new UserAdminService();
    private List<UserRow> _rows = new List<UserRow>();
    private ICollectionView? _view;
    private UserRow? _editing; // null = Add mode
    private bool _loaded;

    public UsersView()
    {
        InitializeComponent();
        ClearEditor();
    }

    private async void View_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        await Reload(null);
    }

    /// <summary>Loads the list and selects <paramref name="selectId"/> (Add mode when null or not found).</summary>
    private async Task Reload(int? selectId)
    {
        _rows = await ViewHelper.Run(Root, () => _service.List()) ?? _rows;
        UsersGrid.ItemsSource = _rows;
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = Matches;

        UserRow? selected = _rows.FirstOrDefault(r => r.Id == selectId);
        if (selected != null)
        {
            UsersGrid.SelectedItem = selected; // Grid_SelectionChanged fills the editor
            UsersGrid.ScrollIntoView(selected);
        }
        else
        {
            ClearEditor();
        }
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UsersGrid.SelectedItem is UserRow row)
        {
            FillEditor(row);
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        UsersGrid.SelectedItem = null;
        ClearEditor();
        UsernameBox.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Password != ConfirmBox.Password)
        {
            ViewHelper.SetMessage(ErrorText, "The password and its confirmation do not match.");
            return;
        }

        UserRole role = TeacherRadio.IsChecked == true ? UserRole.Teacher : UserRole.Student;
        AdminResult? result = await ViewHelper.Run(Root, () => _editing == null
            ? _service.Create(role, UsernameBox.Text, FullNameBox.Text, EmailBox.Text, PasswordBox.Password)
            : _service.Update(_editing.Id, FullNameBox.Text, EmailBox.Text, PasswordBox.Password));
        if (result == null)
        {
            return;
        }
        if (!result.Ok)
        {
            ViewHelper.SetMessage(ErrorText, result.Error);
            return;
        }
        await Reload(result.Id);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null)
        {
            return;
        }
        if (!ViewHelper.Confirm(this, "Are you sure you want to delete the " + _editing.Role.ToString().ToLowerInvariant() + " " + _editing.Username + " (" + _editing.FullName + ")?"))
        {
            return;
        }

        int id = _editing.Id;
        AdminResult? result = await ViewHelper.Run(Root, () => _service.Delete(id));
        if (result == null)
        {
            return;
        }
        if (!result.Ok)
        {
            ViewHelper.Warn(this, result.Error!);
            return;
        }
        await Reload(null);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await Reload(_editing?.Id);
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        _view?.Refresh();
    }

    private bool Matches(object item)
    {
        UserRow row = (UserRow)item;
        if (RoleFilter.SelectedIndex == 1 && row.Role != UserRole.Teacher)
        {
            return false;
        }
        if (RoleFilter.SelectedIndex == 2 && row.Role != UserRole.Student)
        {
            return false;
        }
        string search = SearchBox.Text.Trim();
        return search.Length == 0
            || row.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Email.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Edit mode: shows the selected user; role and username locked.</summary>
    private void FillEditor(UserRow row)
    {
        _editing = row;
        EditorTitle.Text = "Edit user " + row.Username;
        StudentRadio.IsChecked = row.Role == UserRole.Student;
        TeacherRadio.IsChecked = row.Role == UserRole.Teacher;
        StudentRadio.IsEnabled = false;
        TeacherRadio.IsEnabled = false;
        UsernameBox.Text = row.Username;
        UsernameBox.IsReadOnly = true;
        FullNameBox.Text = row.FullName;
        EmailBox.Text = row.Email;
        PasswordBox.Clear();
        ConfirmBox.Clear();
        PasswordLabel.Text = "New password:";
        PasswordHint.Visibility = Visibility.Visible;
        DeleteButton.Visibility = Visibility.Visible;
        ViewHelper.SetMessage(ErrorText, null);
    }

    /// <summary>Add mode: empty fields, role and username editable, password required.</summary>
    private void ClearEditor()
    {
        _editing = null;
        EditorTitle.Text = "New user";
        StudentRadio.IsChecked = true;
        StudentRadio.IsEnabled = true;
        TeacherRadio.IsEnabled = true;
        UsernameBox.Clear();
        UsernameBox.IsReadOnly = false;
        FullNameBox.Clear();
        EmailBox.Clear();
        PasswordBox.Clear();
        ConfirmBox.Clear();
        PasswordLabel.Text = "Password:";
        PasswordHint.Visibility = Visibility.Collapsed;
        DeleteButton.Visibility = Visibility.Collapsed;
        ViewHelper.SetMessage(ErrorText, null);
    }
}
