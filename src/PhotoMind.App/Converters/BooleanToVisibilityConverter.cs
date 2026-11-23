using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PhotoMind.App.Converters;

/// <summary>Maps <c>true</c> to <see cref="Visibility.Visible"/> and <c>false</c> to <see cref="Visibility.Collapsed"/>.</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
