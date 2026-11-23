using System.Globalization;
using System.Windows.Data;

namespace PhotoMind.App.Converters;

/// <summary>Inverts a boolean (used to enable/disable controls based on an "is busy" flag).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not bool b || !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not bool b || !b;
}
