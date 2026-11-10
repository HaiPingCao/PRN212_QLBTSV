using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QLBT.Server.Admin;
using QLBT.Server.Domain;
using QLBT.Shared;

namespace QLBT.Server.Views;

/// <summary>
/// Users tab: editor on top, list below. Selecting a row edits it; "+ New" switches the editor to Add mode.
/// Username and role are locked after creation; a filled password field resets the password.
/// </summary>
public sealed partial class UsersView : UserControl
{
    private readonly UserAdminService _service = new UserAdminService();
    private List<UserRow> _rows = new List<UserRow>();
    private ICollectionView? _view;
    private UserRow? _editing; // null = Add mode
    private bool _loaded;
    private bool _filling = true; // editor is being filled by code (or still initializing): ignore change events
    private bool _dirty;

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

    /// <summary>Loads the list and selects <paramref name="selectId"/> (or keeps the editor as it is when null).</summary>
    private async Task Reload(int? selectId)
    {
        SetBusy(true);
        try
        {
            _rows = await _service.List();
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
        }
        finally
        {
            SetBusy(false);
        }

        _filling = true;
        UsersGrid.ItemsSource = _rows;
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = Matches;
        _filling = false;

        UserRow? selected = selectId == null ? null : _rows.FirstOrDefault(r => r.Id == selectId);
        if (selected != null)
        {
            Select(selected);
        }
        else if (_editing != null)
        {
            // The edited row was deleted (or vanished): back to Add mode.
            ClearEditor();
        }
        UpdateFooter();
    }

    private void Select(UserRow row)
    {
        _filling = true;
        UsersGrid.SelectedItem = row;
        UsersGrid.ScrollIntoView(row);
        _filling = false;
        _editing = row;
        FillEditor();
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_filling || UsersGrid.SelectedItem is not UserRow row || row == _editing)
        {
            return;
        }
        if (!ViewHelper.ConfirmDiscard(this, _dirty))
        {
            // Put the selection back on the row being edited.
            _filling = true;
            UsersGrid.SelectedItem = _editing;
            _filling = false;
            return;
        }
        _editing = row;
        FillEditor();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        StartNew();
    }

    private void StartNew()
    {
        if (!ViewHelper.ConfirmDiscard(this, _dirty))
        {
            return;
        }
        _filling = true;
        UsersGrid.SelectedItem = null;
        _filling = false;
        ClearEditor();
        UsernameBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_editing != null)
        {
            FillEditor();
        }
        else
        {
            ClearEditor();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await Save();
    }

    private async Task Save()
    {
        if (PasswordBox.Password != ConfirmBox.Password)
        {
            ViewHelper.SetMessage(ErrorText, "Password and confirmation do not match.");
            return;
        }

        AdminResult result;
        SetBusy(true);
        try
        {
            if (_editing == null)
            {
                UserRole role = TeacherRadio.IsChecked == true ? UserRole.Teacher : UserRole.Student;
                result = await _service.Create(role, UsernameBox.Text, FullNameBox.Text, EmailBox.Text, PasswordBox.Password);
            }
            else
            {
                result = await _service.Update(_editing.Id, FullNameBox.Text, EmailBox.Text, PasswordBox.Password);
            }
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
            return;
        }
        finally
        {
            SetBusy(false);
        }

        if (!result.Ok)
        {
            ViewHelper.SetMessage(ErrorText, result.Error);
            return;
        }
        _dirty = false;
        await Reload(result.Id);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        await Delete();
    }

    private async Task Delete()
    {
        if (_editing == null || _editing.ClassCount > 0)
        {
            return;
        }
        if (!ViewHelper.Confirm(this, "Delete " + _editing.Role.ToString().ToLowerInvariant() + " " + _editing.Username + " (" + _editing.FullName + ")?"))
        {
            return;
        }

        AdminResult result;
        SetBusy(true);
        try
        {
            result = await _service.Delete(_editing.Id);
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
            return;
        }
        finally
        {
            SetBusy(false);
        }

        if (!result.Ok)
        {
            ViewHelper.Warn(this, result.Error!);
            return;
        }
        _dirty = false;
        await Reload(null);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await Refresh();
    }

    private async Task Refresh()
    {
        if (!ViewHelper.ConfirmDiscard(this, _dirty))
        {
            return;
        }
        _dirty = false;
        await Reload(_editing?.Id);
    }

    private void Field_Changed(object sender, RoutedEventArgs e)
    {
        if (_filling)
        {
            return;
        }
        _dirty = true;
        DirtyText.Visibility = Visibility.Visible;
        ViewHelper.SetMessage(ErrorText, null);
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_view == null)
        {
            return;
        }
        _view.Refresh();
        UpdateFooter();
    }

    private async void View_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            e.Handled = true;
            StartNew();
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            e.Handled = true;
            await Save();
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            await Refresh();
        }
    }

    private async void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && DeleteButton.IsEnabled)
        {
            e.Handled = true;
            await Delete();
        }
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
    private void FillEditor()
    {
        UserRow row = _editing!;
        _filling = true;
        EditorTitle.Text = "Edit user " + row.Username;
        StudentRadio.IsChecked = row.Role == UserRole.Student;
        TeacherRadio.IsChecked = row.Role == UserRole.Teacher;
        UsernameBox.Text = row.Username;
        FullNameBox.Text = row.FullName;
        EmailBox.Text = row.Email;
        PasswordBox.Clear();
        ConfirmBox.Clear();
        _filling = false;

        StudentRadio.IsEnabled = false;
        TeacherRadio.IsEnabled = false;
        RoleLockedText.Visibility = Visibility.Visible;
        UsernameBox.IsReadOnly = true;
        PasswordLabel.Text = "New password:";
        PasswordHint.Visibility = Visibility.Visible;

        bool hasClasses = row.ClassCount > 0;
        DeleteButton.Visibility = Visibility.Visible;
        DeleteButton.IsEnabled = !hasClasses;
        DeleteButton.ToolTip = hasClasses ? "Has classes: suspend the user in the class instead" : "Delete this user (Del)";
        ViewHelper.SetMessage(InfoText, hasClasses
            ? "ℹ " + row.Username + " has " + row.ClassCount + " class(es), so the account cannot be deleted. Suspend them in the class instead."
            : null);
        ResetState();
    }

    /// <summary>Add mode: empty fields, role and username editable, password required.</summary>
    private void ClearEditor()
    {
        _editing = null;
        _filling = true;
        EditorTitle.Text = "New user";
        StudentRadio.IsChecked = true;
        UsernameBox.Clear();
        FullNameBox.Clear();
        EmailBox.Clear();
        PasswordBox.Clear();
        ConfirmBox.Clear();
        _filling = false;

        StudentRadio.IsEnabled = true;
        TeacherRadio.IsEnabled = true;
        RoleLockedText.Visibility = Visibility.Collapsed;
        UsernameBox.IsReadOnly = false;
        PasswordLabel.Text = "Password:";
        PasswordHint.Visibility = Visibility.Collapsed;
        DeleteButton.Visibility = Visibility.Collapsed;
        ViewHelper.SetMessage(InfoText, null);
        ResetState();
    }

    private void ResetState()
    {
        _dirty = false;
        DirtyText.Visibility = Visibility.Collapsed;
        ViewHelper.SetMessage(ErrorText, null);
    }

    private void SetBusy(bool busy)
    {
        Root.IsEnabled = !busy;
        if (busy)
        {
            FooterText.Text = "Loading...";
        }
        else
        {
            UpdateFooter();
        }
    }

    private void UpdateFooter()
    {
        int teachers = _rows.Count(r => r.Role == UserRole.Teacher);
        string text = _rows.Count + " users (" + teachers + " teachers, " + (_rows.Count - teachers) + " students)";
        int shown = UsersGrid.Items.Count;
        if (shown != _rows.Count)
        {
            text = "Showing " + shown + " of " + text;
        }
        FooterText.Text = text;
    }
}
