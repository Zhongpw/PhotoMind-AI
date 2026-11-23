using CommunityToolkit.Mvvm.ComponentModel;
using PhotoMind.Core.Enums;

namespace PhotoMind.App.ViewModels;

/// <summary>A checkable image-format filter shown on the scan page.</summary>
public sealed partial class FormatFilterItem : ObservableObject
{
    public FormatFilterItem(ImageFormat format, string displayName, bool isChecked)
    {
        Format = format;
        DisplayName = displayName;
        IsChecked = isChecked;
    }

    public ImageFormat Format { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    private bool _isChecked;
}
