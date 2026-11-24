using PhotoMind.Core.Enums;
using PhotoMind.Infrastructure.ImageProcessing;

namespace PhotoMind.Tests.ImageProcessing;

public class ImageFormatDetectorTests
{
    private static byte[] Bytes(params int[] values) => values.Select(v => (byte)v).ToArray();

    [Fact]
    public void Detect_JpegMagic_ReturnsJpeg()
    {
        var detector = new ImageFormatDetector();
        var header = Bytes(0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46);
        Assert.Equal(ImageFormat.Jpeg, detector.Detect(header));
    }

    [Fact]
    public void Detect_PngMagic_ReturnsPng()
    {
        var detector = new ImageFormatDetector();
        var header = Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
        Assert.Equal(ImageFormat.Png, detector.Detect(header));
    }

    [Fact]
    public void Detect_WebpMagic_ReturnsWebp()
    {
        var detector = new ImageFormatDetector();
        var header = Bytes(0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50);
        Assert.Equal(ImageFormat.WebP, detector.Detect(header));
    }

    [Fact]
    public void Detect_BmpMagic_ReturnsBmp()
    {
        var detector = new ImageFormatDetector();
        var header = Bytes(0x42, 0x4D, 0x00, 0x00, 0x00, 0x00);
        Assert.Equal(ImageFormat.Bmp, detector.Detect(header));
    }

    [Fact]
    public void Detect_Gif89aMagic_ReturnsGif()
    {
        var detector = new ImageFormatDetector();
        var header = System.Text.Encoding.ASCII.GetBytes("GIF89a");
        Assert.Equal(ImageFormat.Gif, detector.Detect(header));
    }

    [Fact]
    public void Detect_TiffLittleEndian_ReturnsTiff()
    {
        var detector = new ImageFormatDetector();
        var header = Bytes(0x49, 0x49, 0x2A, 0x00);
        Assert.Equal(ImageFormat.Tiff, detector.Detect(header));
    }

    [Fact]
    public void Detect_HeicFtyp_ReturnsHeic()
    {
        var detector = new ImageFormatDetector();
        // ISO BMFF: bytes 0-3 size, bytes 4-7 "ftyp", bytes 8-11 major brand
        var header = Bytes(0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63);
        Assert.Equal(ImageFormat.Heic, detector.Detect(header));
    }

    [Theory]
    [InlineData(0x49, 0x49, 0x2A, 0x00, 0x10, 0x00, 0x00, 0x00, 0x43, 0x52, 0x02, 0x00)] // CR2
    [InlineData(0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, 0x4E, 0x45, 0x46, 0x00)] // NEF
    [InlineData(0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x53, 0x6F, 0x6E, 0x79)] // ARW (Sony)
    [InlineData(0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, 0x44, 0x4E, 0x47, 0x00)] // DNG
    public void Detect_TiffBasedRaw_ReturnsRaw(int b0, int b1, int b2, int b3, int b4, int b5, int b6, int b7, int b8, int b9, int b10, int b11)
    {
        var detector = new ImageFormatDetector();
        var header = Bytes(b0, b1, b2, b3, b4, b5, b6, b7, b8, b9, b10, b11);
        Assert.Equal(ImageFormat.Raw, detector.Detect(header));
    }

    [Fact]
    public void Detect_FujiRafMagic_ReturnsRaw()
    {
        var detector = new ImageFormatDetector();
        var header = System.Text.Encoding.ASCII.GetBytes("FUJIFILMCCD-RAW");
        Assert.Equal(ImageFormat.Raw, detector.Detect(header));
    }

    [Theory]
    [InlineData(0x49, 0x49, 0x52, 0x4F)] // IIRO (Olympus ORF)
    [InlineData(0x4D, 0x4D, 0x4F, 0x52)] // MMOR (Olympus ORF)
    [InlineData(0x49, 0x49, 0x55, 0x00)] // IIU\0 (Panasonic RW2)
    public void Detect_DistinctHeaderRaw_ReturnsRaw(int b0, int b1, int b2, int b3)
    {
        var detector = new ImageFormatDetector();
        var header = Bytes(b0, b1, b2, b3, 0x08, 0x00, 0x00, 0x00);
        Assert.Equal(ImageFormat.Raw, detector.Detect(header));
    }

    [Fact]
    public void Detect_RandomBytes_ReturnsUnknown()
    {
        var detector = new ImageFormatDetector();
        var header = Bytes(0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04);
        Assert.Equal(ImageFormat.Unknown, detector.Detect(header));
    }

    [Fact]
    public void Detect_EmptyHeader_ReturnsUnknown()
    {
        var detector = new ImageFormatDetector();
        Assert.Equal(ImageFormat.Unknown, detector.Detect(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void IsSupported_Stage1Formats_ReturnsTrue()
    {
        var detector = new ImageFormatDetector();
        Assert.True(detector.IsSupported(ImageFormat.Jpeg));
        Assert.True(detector.IsSupported(ImageFormat.Png));
        Assert.True(detector.IsSupported(ImageFormat.WebP));
        Assert.True(detector.IsSupported(ImageFormat.Bmp));
        Assert.True(detector.IsSupported(ImageFormat.Gif));
    }

    [Fact]
    public void IsSupported_ExtendedFormats_ReturnsTrue()
    {
        var detector = new ImageFormatDetector();
        Assert.True(detector.IsSupported(ImageFormat.Tiff));
        Assert.True(detector.IsSupported(ImageFormat.Heic));
        Assert.True(detector.IsSupported(ImageFormat.Raw));
    }

    [Fact]
    public void IsSupported_Unknown_ReturnsFalse()
    {
        var detector = new ImageFormatDetector();
        Assert.False(detector.IsSupported(ImageFormat.Unknown));
    }
}
