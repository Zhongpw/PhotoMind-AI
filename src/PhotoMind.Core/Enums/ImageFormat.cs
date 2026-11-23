namespace PhotoMind.Core.Enums;

/// <summary>Image container/encoding formats the app can recognise (detected from file content, not extension).</summary>
public enum ImageFormat
{
    Unknown = 0,
    Jpeg = 1,
    Png = 2,
    WebP = 3,
    Bmp = 4,
    Gif = 5,
    Tiff = 6,
    Heic = 7,
    Raw = 8,
}
