using System.Windows.Controls;
using PhotoMind.App.ViewModels;

namespace PhotoMind.App.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is HomeViewModel viewModel)
                await viewModel.LoadAsync();
        };
    }
}
