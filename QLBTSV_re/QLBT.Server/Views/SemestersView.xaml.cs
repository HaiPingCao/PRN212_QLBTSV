using System.Windows;
using System.Windows.Controls;
using QLBT.Server.Admin;
using QLBT.Server.Domain;

namespace QLBT.Server.Views;

/// <summary>Semesters tab: editor on top, list below. A semester with classes cannot be deleted.</summary>
public sealed partial class SemestersView : UserControl
{
    private readonly SemesterAdminService _service = new SemesterAdminService();
    private List<SemesterRow> _rows = new List<SemesterRow>();
    private SemesterRow? _editing; // null = Add mode
    private bool _loaded;

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
        _rows = await ViewHelper.Run(Root, () => _service.List()) ?? _rows;
        SemestersGrid.ItemsSource = _rows;
        SemesterRow? selected = _rows.FirstOrDefault(r => r.Id == selectId);
        if (selected != null)
        {
            SemestersGrid.SelectedItem = selected; // Grid_SelectionChanged fills the editor
            SemestersGrid.ScrollIntoView(selected);
        }
        else
        {
            ClearEditor();
        }
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SemestersGrid.SelectedItem is SemesterRow row)
        {
            FillEditor(row);
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        SemestersGrid.SelectedItem = null;
        ClearEditor();
        NameBox.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        DateOnly? start = ViewHelper.ToDateOnly(StartPicker.SelectedDate);
        DateOnly? end = ViewHelper.ToDateOnly(EndPicker.SelectedDate);
        if (start == null || end == null)
        {
            ViewHelper.SetMessage(ErrorText, "Please enter both a start date and an end date.");
            return;
        }

        DateOnly startDate = start.Value;
        DateOnly endDate = end.Value;
        AdminResult? result = await ViewHelper.Run(Root, () => _editing == null
            ? _service.Create(NameBox.Text, startDate, endDate)
            : _service.Update(_editing.Id, NameBox.Text, startDate, endDate));
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
        if (!ViewHelper.Confirm(this, "Are you sure you want to delete the semester " + _editing.Name + "?"))
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

    /// <summary>Edit mode. An ended semester is shown read-only; a running one shows a warning.</summary>
    private void FillEditor(SemesterRow row)
    {
        _editing = row;
        bool ended = row.Status == SemesterStatus.Ended;
        EditorTitle.Text = (ended ? "Semester " : "Edit semester ") + row.Name;
        NameBox.Text = row.Name;
        StartPicker.SelectedDate = ViewHelper.ToDateTime(row.StartDate);
        EndPicker.SelectedDate = ViewHelper.ToDateTime(row.EndDate);
        SetLocked(ended);
        DeleteButton.Visibility = Visibility.Visible;
        string? warning = row.Status switch
        {
            SemesterStatus.Ended => "This semester has ended. Its information can no longer be changed.",
            SemesterStatus.Running => "This semester has already started.",
            _ => null
        };
        ViewHelper.SetMessage(WarningText, warning);
        ViewHelper.SetMessage(ErrorText, null);
    }

    private void ClearEditor()
    {
        _editing = null;
        EditorTitle.Text = "New semester";
        NameBox.Clear();
        StartPicker.SelectedDate = null;
        EndPicker.SelectedDate = null;
        SetLocked(false);
        DeleteButton.Visibility = Visibility.Collapsed;
        ViewHelper.SetMessage(WarningText, null);
        ViewHelper.SetMessage(ErrorText, null);
    }

    private void SetLocked(bool locked)
    {
        NameBox.IsReadOnly = locked;
        StartPicker.IsEnabled = !locked;
        EndPicker.IsEnabled = !locked;
        SaveButton.IsEnabled = !locked;
        DeleteButton.IsEnabled = !locked;
    }
}
