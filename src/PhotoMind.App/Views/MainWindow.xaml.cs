using System.Windows;
using PhotoMind.App.ViewModels;

namespace PhotoMind.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
