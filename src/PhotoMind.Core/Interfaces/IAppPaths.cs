namespace PhotoMind.Core.Interfaces;

/// <summary>Resolves local data-directory locations (DB, thumbnails, logs).</summary>
public interface IAppPaths
{
    string DataDirectory { get; }
    string DatabasePath { get; }
    string ThumbnailDirectory { get; }
    string LogDirectory { get; }

    /// <summary>Creates the data, thumbnail and log directories if they do not yet exist.</summary>
    void EnsureCreated();
}
