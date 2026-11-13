using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QLBT.Server.Admin;
using QLBT.Server.Domain;

namespace QLBT.Server.Views;

/// <summary>Courses tab (môn học): editor on top, list below. A course used by any class cannot be deleted.</summary>
public sealed partial class CoursesView : UserControl
{
    private readonly CourseAdminService _service = new CourseAdminService();
    private List<CourseRow> _rows = new List<CourseRow>();
    private ICollectionView? _view;
    private CourseRow? _editing; // null = Add mode
    private bool _loaded;

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
        _rows = await ViewHelper.Run(Root, () => _service.List()) ?? _rows;
        CoursesGrid.ItemsSource = _rows;
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = Matches;

        CourseRow? selected = _rows.FirstOrDefault(r => r.Id == selectId);
        if (selected != null)
        {
            CoursesGrid.SelectedItem = selected; // Grid_SelectionChanged fills the editor
            CoursesGrid.ScrollIntoView(selected);
        }
        else
        {
            ClearEditor();
        }
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CoursesGrid.SelectedItem is CourseRow row)
        {
            FillEditor(row);
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        CoursesGrid.SelectedItem = null;
        ClearEditor();
        CodeBox.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        AdminResult? result = await ViewHelper.Run(Root, () => _editing == null
            ? _service.Create(CodeBox.Text, NameBox.Text)
            : _service.Update(_editing.Id, CodeBox.Text, NameBox.Text));
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
        if (!ViewHelper.Confirm(this, "Are you sure you want to delete the course " + _editing.Code + " - " + _editing.Name + "?"))
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

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _view?.Refresh();
    }

    private bool Matches(object item)
    {
        CourseRow row = (CourseRow)item;
        string search = SearchBox.Text.Trim();
        return search.Length == 0
            || row.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void FillEditor(CourseRow row)
    {
        _editing = row;
        EditorTitle.Text = "Edit course " + row.Code;
        CodeBox.Text = row.Code;
        NameBox.Text = row.Name;
        DeleteButton.Visibility = Visibility.Visible;
        ViewHelper.SetMessage(ErrorText, null);
    }

    private void ClearEditor()
    {
        _editing = null;
        EditorTitle.Text = "New course";
        CodeBox.Clear();
        NameBox.Clear();
        DeleteButton.Visibility = Visibility.Collapsed;
        ViewHelper.SetMessage(ErrorText, null);
    }
}
