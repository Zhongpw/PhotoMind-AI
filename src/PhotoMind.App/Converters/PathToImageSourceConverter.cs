using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PhotoMind.App.Converters;

/// <summary>
/// Converts a thumbnail file path to a frozen, cached <see cref="BitmapImage"/>. Returns null for missing/empty
/// paths (the view falls back to a placeholder). Decoding is capped so only visible thumbnails are materialised.
/// </summary>
public sealed class PathToImageSourceConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxCacheEntries = 2000;
    private const int DecodeWidth = 256;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        if (Cache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = DecodeWidth;
            bitmap.EndInit();
            bitmap.Freeze();

            if (Cache.Count >= MaxCacheEntries)
                Cache.Clear();
            Cache[path] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
