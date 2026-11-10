using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QLBT.Server.Admin;
using QLBT.Server.Domain;

namespace QLBT.Server.Views;

/// <summary>Multi-select picker of students who are not in the class yet. Saves on Add and closes with DialogResult = true.</summary>
public sealed partial class AddStudentsDialog : Window
{
    private readonly ClassRow _class;
    private readonly ClassAdminService _service;
    private List<SelectableStudent> _students = new List<SelectableStudent>();
    private ICollectionView? _view;

    public AddStudentsDialog(ClassRow targetClass, ClassAdminService service)
    {
        InitializeComponent();
        _class = targetClass;
        _service = service;
        Title = "Add students to " + targetClass.Name + " · " + targetClass.CourseCode;
        TitleText.Text = "Add students to " + targetClass.Name + " · " + targetClass.CourseCode + " (" + targetClass.SemesterName + ")";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _students = await _service.ListAvailableStudents(_class.Id);
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
            Close();
            return;
        }

        StudentsGrid.ItemsSource = _students;
        _view = CollectionViewSource.GetDefaultView(_students);
        _view.Filter = Matches;
        UpdateSelected();
        SearchBox.Focus();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _view?.Refresh();
        UpdateSelected();
    }

    /// <summary>Header checkbox: selects or clears every visible row.</summary>
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        bool select = SelectAllBox.IsChecked == true;
        foreach (SelectableStudent student in Visible())
        {
            student.IsSelected = select;
        }
        UpdateSelected();
    }

    private void RowCheck_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelected();
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        List<int> ids = _students.Where(s => s.IsSelected).Select(s => s.Id).ToList();
        if (ids.Count == 0)
        {
            ViewHelper.SetMessage(ErrorText, "Select at least one student.");
            return;
        }

        AdminResult result;
        IsEnabled = false;
        try
        {
            result = await _service.AddStudents(_class.Id, ids);
        }
        catch (Exception ex)
        {
            ViewHelper.ShowError(this, ex);
            return;
        }
        finally
        {
            IsEnabled = true;
        }

        if (!result.Ok)
        {
            ViewHelper.SetMessage(ErrorText, result.Error);
            return;
        }
        DialogResult = true;
    }

    private bool Matches(object item)
    {
        SelectableStudent student = (SelectableStudent)item;
        string search = SearchBox.Text.Trim();
        return search.Length == 0
            || student.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
            || student.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || student.Email.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<SelectableStudent> Visible()
    {
        return _view == null ? _students : _view.Cast<SelectableStudent>();
    }

    private void UpdateSelected()
    {
        int selected = _students.Count(s => s.IsSelected);
        SelectedText.Text = _students.Count == 0
            ? "Every student is already in this class."
            : selected + " selected · " + _students.Count + " available";
        AddButton.IsEnabled = selected > 0;
        List<SelectableStudent> visible = Visible().ToList();
        SelectAllBox.IsChecked = visible.Count > 0 && visible.All(s => s.IsSelected);
        ViewHelper.SetMessage(ErrorText, null);
    }
}
