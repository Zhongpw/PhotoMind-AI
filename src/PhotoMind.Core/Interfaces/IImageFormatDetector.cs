using PhotoMind.Core.Enums;

namespace PhotoMind.Core.Interfaces;

/// <summary>Detects an image's format from its leading bytes (never trusts the file extension alone).</summary>
public interface IImageFormatDetector
{
    /// <summary>Inspects the leading bytes and returns the detected format.</summary>
    ImageFormat Detect(ReadOnlySpan<byte> header);

    /// <summary>Whether the given format is supported by the current pipeline.</summary>
    bool IsSupported(ImageFormat format);

    /// <summary>Set of file extensions (lowercase, without dot) that should be considered candidates.</summary>
    IReadOnlySet<string> CandidateExtensions { get; }
}
