using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QLBT.Server.Admin;
using QLBT.Server.Domain;

namespace QLBT.Server.Views;

/// <summary>
/// Semesters tab: editor on top, list below. After the start the start date is locked and the end date
/// can only move later; started semesters and semesters with classes cannot be deleted.
/// </summary>
public sealed partial class SemestersView : UserControl
{
    private readonly SemesterAdminService _service = new SemesterAdminService();
    private List<SemesterRow> _rows = new List<SemesterRow>();
    private SemesterRow? _editing; // null = Add mode
    private bool _loaded;
    private bool _filling = true;  // editor is being filled by code (or still initializing): ignore change events
    private bool _dirty;

    public SemestersView()
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
        SemestersGrid.ItemsSource = _rows;
        _filling = false;

        SemesterRow? selected = selectId == null ? null : _rows.FirstOrDefault(r => r.Id == selectId);
        if (selected != null)
        {
            _filling = true;
            SemestersGrid.SelectedItem = selected;
            SemestersGrid.ScrollIntoView(selected);
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
        if (_filling || SemestersGrid.SelectedItem is not SemesterRow row || row == _editing)
        {
            return;
        }
        if (!ViewHelper.ConfirmDiscard(this, _dirty))
        {
            _filling = true;
            SemestersGrid.SelectedItem = _editing;
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
        SemestersGrid.SelectedItem = null;
        _filling = false;
        ClearEditor();
        NameBox.Focus();
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
        DateOnly? start = ViewHelper.ToDateOnly(StartPicker.SelectedDate);
        DateOnly? end = ViewHelper.ToDateOnly(EndPicker.SelectedDate);
        if (start == null || end == null)
        {
            ViewHelper.SetMessage(ErrorText, "Start date and end date are required.");
            return;
        }

        AdminResult result;
        SetBusy(true);
        try
        {
            result = _editing == null
                ? await _service.Create(NameBox.Text, start.Value, end.Value)
                : await _service.Update(_editing.Id, NameBox.Text, start.Value, end.Value);
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
        if (!ViewHelper.Confirm(this, "Delete semester " + _editing.Name + "?"))
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

    private void FillEditor()
    {
        SemesterRow row = _editing!;
        _filling = true;
        EditorTitle.Text = "Edit semester " + row.Name;
        NameBox.Text = row.Name;
        StartPicker.SelectedDate = ViewHelper.ToDateTime(row.StartDate);
        EndPicker.SelectedDate = ViewHelper.ToDateTime(row.EndDate);
        StatusText.Text = row.StatusText;
        _filling = false;

        bool started = row.HasStarted;
        StartPicker.IsEnabled = !started;
        StartPicker.ToolTip = started ? "The semester has started: the start date is locked" : null;
        // After the start the end date can only move later.
        EndPicker.DisplayDateStart = started ? ViewHelper.ToDateTime(row.EndDate) : null;

        DeleteButton.Visibility = Visibility.Visible;
        string? deleteBlocked = started
            ? "The semester has started and cannot be deleted"
            : row.ClassCount > 0 ? "The semester has " + row.ClassCount + " class(es): delete them first" : null;
        DeleteButton.IsEnabled = deleteBlocked == null;
        DeleteButton.ToolTip = deleteBlocked ?? "Delete this semester (Del)";

        string? info = null;
        if (started)
        {
            info = "ℹ Semester has started: the start date is locked and the end date can only move later.";
        }
        else if (row.ClassCount > 0)
        {
            info = "ℹ Delete the " + row.ClassCount + " class(es) of this semester before deleting it.";
        }
        ViewHelper.SetMessage(InfoText, info);
        ResetState();
    }

    private void ClearEditor()
    {
        _editing = null;
        _filling = true;
        EditorTitle.Text = "New semester";
        NameBox.Clear();
        StartPicker.SelectedDate = null;
        EndPicker.SelectedDate = null;
        StatusText.Text = "-";
        _filling = false;

        StartPicker.IsEnabled = true;
        StartPicker.ToolTip = null;
        EndPicker.DisplayDateStart = null;
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
        FooterText.Text = _rows.Count + " semesters";
    }
}
