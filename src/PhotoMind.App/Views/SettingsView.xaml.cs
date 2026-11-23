using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PhotoMind.App.ViewModels;

namespace PhotoMind.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SettingsViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is SettingsViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            SyncPasswordBox(newVm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is SettingsViewModel vm &&
            (e.PropertyName == nameof(SettingsViewModel.ShowApiKey) || e.PropertyName == nameof(SettingsViewModel.ApiKeyInput)))
        {
            SyncPasswordBox(vm);
        }
    }

    private void SyncPasswordBox(SettingsViewModel vm)
    {
        if (!vm.ShowApiKey && ApiKeyPasswordBox.Password != vm.ApiKeyInput)
            ApiKeyPasswordBox.Password = vm.ApiKeyInput ?? string.Empty;
    }

    private void OnApiKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && ApiKeyPasswordBox.Password != vm.ApiKeyInput)
            vm.ApiKeyInput = ApiKeyPasswordBox.Password;
    }
}
