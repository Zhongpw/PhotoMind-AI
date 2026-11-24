using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PhotoMind.App.ViewModels;

namespace PhotoMind.App.Views;

public partial class ScanView : UserControl
{
    public ScanView() => InitializeComponent();

    private void OnRemoveFromTaskClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ScanViewModel vm)
            return;

        var selected = ImageListBox.SelectedItems.Cast<ImageItemViewModel>().ToList();
        if (selected.Count == 0)
            return;

        _ = vm.RemoveFromTaskAsync(selected);
    }

    private void OnImageListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var container = ItemsControl.ContainerFromElement(ImageListBox, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (container is null)
            return;

        // Right-click on an unselected item selects just that item (single removal);
        // right-click on an already-selected item keeps the current multi-selection (batch removal).
        if (!container.IsSelected)
        {
            ImageListBox.SelectedItems.Clear();
            container.IsSelected = true;
        }

        container.Focus();
        e.Handled = true;
    }
}
