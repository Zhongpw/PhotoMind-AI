namespace PhotoMind.Core.Exceptions;

/// <summary>Base exception for all PhotoMind-specific failures.</summary>
public abstract class PhotoMindException : Exception
{
    protected PhotoMindException(string message) : base(message) { }
    protected PhotoMindException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Thrown when an image file cannot be read or decoded (corrupt, locked, unsupported).</summary>
public sealed class ImageReadException : PhotoMindException
{
    public string? FilePath { get; }

    public ImageReadException(string message, string? filePath = null, Exception? inner = null)
        : base(message, inner ?? new InvalidOperationException(message))
    {
        FilePath = filePath;
    }
}

/// <summary>Thrown when a directory cannot be enumerated (missing, no permission, I/O failure).</summary>
public sealed class DirectoryScanException : PhotoMindException
{
    public string? DirectoryPath { get; }

    public DirectoryScanException(string message, string? directoryPath = null, Exception? inner = null)
        : base(message, inner ?? new InvalidOperationException(message))
    {
        DirectoryPath = directoryPath;
    }
}

/// <summary>Thrown when a path fails validation (path traversal, protected directory, recursion loop).</summary>
public sealed class PathValidationException : PhotoMindException
{
    public PathValidationException(string message) : base(message) { }
}
