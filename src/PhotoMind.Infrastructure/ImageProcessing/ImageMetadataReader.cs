using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.ImageProcessing;

/// <summary>Reads pixel dimensions and EXIF capture time via ImageSharp's lightweight Identify path.</summary>
public sealed class ImageMetadataReader : IImageMetadataReader
{
    public Task<ImageMetadata> ReadAsync(string filePath, ImageFormat format, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var info = Image.Identify(filePath);
                if (info is null)
                    throw new ImageReadException("无法识别图片格式或内容", filePath);

                int width = info.Width;
                int height = info.Height;

                DateTime? exifDate = null;
                var exif = info.Metadata.ExifProfile;
                if (exif is not null)
                {
                    // Correctly honour EXIF orientation for the reported dimensions.
                    if (exif.TryGetValue(ExifTag.Orientation, out var orientationValue)
                        && orientationValue?.Value is ushort orientation
                        && orientation is >= 5 and <= 8)
                    {
                        (width, height) = (height, width);
                    }

                    string? raw = null;
                    if (exif.TryGetValue(ExifTag.DateTimeOriginal, out var dateOriginal)
                        && dateOriginal?.Value is string d1 && !string.IsNullOrWhiteSpace(d1))
                    {
                        raw = d1;
                    }
                    else if (exif.TryGetValue(ExifTag.DateTimeDigitized, out var dateDigitized)
                        && dateDigitized?.Value is string d2 && !string.IsNullOrWhiteSpace(d2))
                    {
                        raw = d2;
                    }

                    raw = raw?.Trim('\0', ' ').Trim();
                    if (!string.IsNullOrEmpty(raw)
                        && DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        exifDate = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
                    }
                }

                return new ImageMetadata(width, height, exifDate);
            }
            catch (ImageReadException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new ImageReadException($"读取图片元数据失败：{ex.Message}", filePath, ex);
            }
        }, cancellationToken);
    }
}
