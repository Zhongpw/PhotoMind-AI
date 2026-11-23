using System.Windows;
using System.Windows.Controls;
using PhotoMind.App.ViewModels;

namespace PhotoMind.App.Views;

public partial class CategoryManagementView : UserControl
{
    public CategoryManagementView()
    {
        InitializeComponent();
    }

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is CategoryManagementViewModel vm)
            vm.SelectedNode = e.NewValue as CategoryNodeViewModel;
    }
}
