using System.Globalization;
using ImageMagick;
using Serilog;

namespace PhotoMind.Infrastructure.ImageProcessing;

/// <summary>
/// Decodes the extended formats that ImageSharp cannot handle reliably (TIFF variants, HEIC/HEIF and RAW)
/// via Magick.NET. Produces JPEG output and basic metadata; the original file is never modified.
/// </summary>
public static class MagickImageDecoder
{
    public static bool TryIdentify(string sourcePath, out int width, out int height, out DateTime? exifDate)
    {
        width = 0;
        height = 0;
        exifDate = null;
        try
        {
            using var image = new MagickImage(sourcePath);
            width = (int)image.Width;
            height = (int)image.Height;

            var exif = image.GetExifProfile();
            if (exif is not null)
            {
                if (exif.GetValue(ExifTag.Orientation)?.Value is ushort orientation && orientation is >= 5 and <= 8)
                {
                    (width, height) = (height, width);
                }

                var raw = exif.GetValue(ExifTag.DateTimeOriginal)?.Value as string
                          ?? exif.GetValue(ExifTag.DateTimeDigitized)?.Value as string;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    raw = raw.Trim('\0', ' ').Trim();
                    if (DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        exifDate = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Magick.NET 读取元数据失败: {Path}", sourcePath);
            return false;
        }
    }

    public static bool TryCreateJpeg(string sourcePath, string targetPath, int maxEdge, int quality)
    {
        try
        {
            using var image = new MagickImage(sourcePath);
            image.AutoOrient();
            image.Thumbnail(new MagickGeometry(maxEdge, maxEdge));
            image.Format = MagickFormat.Jpeg;
            image.Quality = quality;
            image.Write(targetPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Magick.NET 解码失败: {Path}", sourcePath);
            return false;
        }
    }
}
