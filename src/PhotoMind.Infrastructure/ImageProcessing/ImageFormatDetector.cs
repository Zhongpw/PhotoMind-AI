using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.ImageProcessing;

/// <summary>
/// Detects an image's format from its leading bytes (magic numbers). The file extension is never trusted alone;
/// it is used only as a cheap candidate filter before this content check runs.
/// </summary>
public sealed class ImageFormatDetector : IImageFormatDetector
{
    private static readonly HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg", "jpeg", "png", "webp", "bmp", "gif",
        "tif", "tiff", "heic", "heif",
        "cr2", "nef", "arw", "dng", "orf", "rw2", "raf", "srw", "pef",
    };

    public IReadOnlySet<string> CandidateExtensions => _extensions;

    public ImageFormat Detect(ReadOnlySpan<byte> h)
    {
        // JPEG: FF D8 FF
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            return ImageFormat.Jpeg;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
            && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A)
            return ImageFormat.Png;

        // WebP: "RIFF" + 4-byte size + "WEBP"
        if (h.Length >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
            && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P')
            return ImageFormat.WebP;

        // BMP: "BM"
        if (h.Length >= 2 && h[0] == 'B' && h[1] == 'M')
            return ImageFormat.Bmp;

        // GIF: "GIF87a" / "GIF89a"
        if (h.Length >= 6 && h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8'
            && (h[4] == '7' || h[4] == '9') && h[5] == 'a')
            return ImageFormat.Gif;

        // RAW with a distinct header: Fuji RAF "FUJIFILM", Olympus ORF "IIRO"/"MMOR", Panasonic RW2 "IIU\0"
        if (h.Length >= 8 && h[0] == 'F' && h[1] == 'U' && h[2] == 'J' && h[3] == 'I'
            && h[4] == 'F' && h[5] == 'I' && h[6] == 'L' && h[7] == 'M')
            return ImageFormat.Raw;
        if (h.Length >= 4
            && ((h[0] == 'I' && h[1] == 'I' && h[2] == 'R' && h[3] == 'O')
                || (h[0] == 'M' && h[1] == 'M' && h[2] == 'O' && h[3] == 'R')
                || (h[0] == 'I' && h[1] == 'I' && h[2] == 'U' && h[3] == 0x00)))
            return ImageFormat.Raw;

        // TIFF (II*\0 or MM\0*). TIFF-based RAW (CR2/NEF/ARW/DNG) share the byte-order marker but carry a
        // camera signature at offset 8, so they are classified as Raw instead of Tiff.
        var isTiffMarker = h.Length >= 4
            && ((h[0] == 0x49 && h[1] == 0x49 && h[2] == 0x2A && h[3] == 0x00)
                || (h[0] == 0x4D && h[1] == 0x4D && h[2] == 0x00 && h[3] == 0x2A));
        if (isTiffMarker)
        {
            if (h.Length >= 12
                && ((h[8] == 'C' && h[9] == 'R')
                    || (h[8] == 'N' && h[9] == 'E' && h[10] == 'F')
                    || (h[8] == 'S' && h[9] == 'o' && h[10] == 'n' && h[11] == 'y')
                    || (h[8] == 'D' && h[9] == 'N' && h[10] == 'G')))
                return ImageFormat.Raw;

            return ImageFormat.Tiff;
        }

        // HEIC/HEIF (ISO BMFF): bytes 4-7 == "ftyp"
        if (h.Length >= 12 && h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p')
            return ImageFormat.Heic;

        return ImageFormat.Unknown;
    }

    public bool IsSupported(ImageFormat format) => format is
        ImageFormat.Jpeg or ImageFormat.Png or ImageFormat.WebP or ImageFormat.Bmp or ImageFormat.Gif
        or ImageFormat.Tiff or ImageFormat.Heic or ImageFormat.Raw;
}
