using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoMind.App.ViewModels;

/// <summary>Placeholder page shown for features that arrive in later phases (classify/review/organise/...).</summary>
public sealed partial class PlaceholderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    public PlaceholderViewModel(string title) => _title = title;
}
