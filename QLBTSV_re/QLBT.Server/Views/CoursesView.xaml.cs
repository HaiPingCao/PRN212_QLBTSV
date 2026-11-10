using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QLBT.Server.Admin;
using QLBT.Server.Domain;

namespace QLBT.Server.Views;

/// <summary>
/// Courses tab (môn học): editor on top, list below. Code and name can always be edited;
/// a course used by any class cannot be deleted.
/// </summary>
public sealed partial class CoursesView : UserControl
{
    private readonly CourseAdminService _service = new CourseAdminService();
    private List<CourseRow> _rows = new List<CourseRow>();
    private ICollectionView? _view;
    private CourseRow? _editing;   // null = Add mode
    private bool _loaded;
    private bool _filling = true;  // editor is being filled by code (or still initializing): ignore change events
    private bool _dirty;

    public CoursesView()
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
        CoursesGrid.ItemsSource = _rows;
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = Matches;
        _filling = false;

        CourseRow? selected = selectId == null ? null : _rows.FirstOrDefault(r => r.Id == selectId);
        if (selected != null)
        {
            _filling = true;
            CoursesGrid.SelectedItem = selected;
            CoursesGrid.ScrollIntoView(selected);
            _filling = false;
            _editing = selected;
            FillEditor();
        }
        else if (_editing != null)
        {
            ClearEditor();
        }
        UpdateFooter();
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_filling || CoursesGrid.SelectedItem is not CourseRow row || row == _editing)
        {
            return;
        }
        if (!ViewHelper.ConfirmDiscard(this, _dirty))
        {
            _filling = true;
            CoursesGrid.SelectedItem = _editing;
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
        CoursesGrid.SelectedItem = null;
        _filling = false;
        ClearEditor();
        CodeBox.Focus();
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
        AdminResult result;
        SetBusy(true);
        try
        {
            result = _editing == null
                ? await _service.Create(CodeBox.Text, NameBox.Text)
                : await _service.Update(_editing.Id, CodeBox.Text, NameBox.Text);
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
        if (_editing == null || !DeleteButton.IsEnabled)
        {
            return;
        }
        if (!ViewHelper.Confirm(this, "Delete course " + _editing.Code + " - " + _editing.Name + "?"))
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

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _view?.Refresh();
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
        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await Delete();
        }
    }

    private bool Matches(object item)
    {
        CourseRow row = (CourseRow)item;
        string search = SearchBox.Text.Trim();
        return search.Length == 0
            || row.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void FillEditor()
    {
        CourseRow row = _editing!;
        _filling = true;
        EditorTitle.Text = "Edit course " + row.Code;
        CodeBox.Text = row.Code;
        NameBox.Text = row.Name;
        _filling = false;

        bool used = row.ClassCount > 0;
        DeleteButton.Visibility = Visibility.Visible;
        DeleteButton.IsEnabled = !used;
        DeleteButton.ToolTip = used ? "Used by " + row.ClassCount + " class(es): cannot be deleted" : "Delete this course (Del)";
        ViewHelper.SetMessage(InfoText, used ? "ℹ " + row.Code + " is used by " + row.ClassCount + " class(es), so it cannot be deleted." : null);
        ResetState();
    }

    private void ClearEditor()
    {
        _editing = null;
        _filling = true;
        EditorTitle.Text = "New course";
        CodeBox.Clear();
        NameBox.Clear();
        _filling = false;

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
        int shown = CoursesGrid.Items.Count;
        FooterText.Text = shown == _rows.Count ? _rows.Count + " courses" : "Showing " + shown + " of " + _rows.Count + " courses";
    }
}
