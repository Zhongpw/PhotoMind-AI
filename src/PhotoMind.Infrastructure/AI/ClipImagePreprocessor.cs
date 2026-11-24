using ImageMagick;
using PhotoMind.Core.Constants;
using PhotoMind.Core.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PhotoMind.Infrastructure.AI;

/// <summary>
/// Prepares an image for CLIP inference: decodes, auto-orients, resizes to fill (shortest edge)
/// then center-crops to <see cref="AiConstants.ClipImageSize"/>², and normalizes each RGB channel
/// with the CLIP mean/std into a flat NCHW float array [1, 3, size, size].
/// </summary>
public static class ClipImagePreprocessor
{
    public static float[] Preprocess(string imagePath, ImageFormat format)
    {
        var size = AiConstants.ClipImageSize;
        var mean = AiConstants.ClipMean;
        var std = AiConstants.ClipStd;
        return format is ImageFormat.Heic or ImageFormat.Raw
            ? PreprocessWithMagick(imagePath, size, mean, std)
            : PreprocessWithImageSharp(imagePath, size, mean, std);
    }

    private static float[] PreprocessWithImageSharp(string path, int size, float[] mean, float[] std)
    {
        using var image = Image.Load<Rgb24>(path);
        image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
        {
            Mode = ResizeMode.Crop,
            Size = new Size(size, size),
        }));

        var data = new float[3, size, size];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < size; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < size; x++)
                {
                    var p = row[x];
                    data[0, y, x] = (p.R / 255f - mean[0]) / std[0];
                    data[1, y, x] = (p.G / 255f - mean[1]) / std[1];
                    data[2, y, x] = (p.B / 255f - mean[2]) / std[2];
                }
            }
        });

        return FlattenNchw(data, size);
    }

    private static float[] PreprocessWithMagick(string path, int size, float[] mean, float[] std)
    {
        using var image = new MagickImage(path);
        image.AutoOrient();

        var geom = new MagickGeometry(size, size) { IgnoreAspectRatio = false, FillArea = true };
        image.Resize(geom);
        image.Crop(size, size, Gravity.Center); // Crop mutates in place (void)

        var rgb = image.ToByteArray(MagickFormat.Rgb); // interleaved R,G,B

        var flat = new float[3 * size * size];
        for (var i = 0; i < size * size; i++)
        {
            var r = rgb[i * 3 + 0];
            var g = rgb[i * 3 + 1];
            var b = rgb[i * 3 + 2];
            flat[0 * size * size + i] = (r / 255f - mean[0]) / std[0];
            flat[1 * size * size + i] = (g / 255f - mean[1]) / std[1];
            flat[2 * size * size + i] = (b / 255f - mean[2]) / std[2];
        }

        return flat;
    }

    private static float[] FlattenNchw(float[,,] data, int size)
    {
        var flat = new float[3 * size * size];
        var idx = 0;
        for (var c = 0; c < 3; c++)
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    flat[idx++] = data[c, y, x];
        return flat;
    }
}
