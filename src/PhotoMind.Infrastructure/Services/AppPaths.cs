using PhotoMind.Core.Constants;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.Services;

/// <summary>
/// Resolves the app's local data directory. The <see cref="AppConstants.DataDirEnvironmentVariable"/> env var
/// overrides the default %LOCALAPPDATA%\PhotoMind AI location (used by tests to isolate data).
/// </summary>
public sealed class AppPaths : IAppPaths
{
    private readonly string _dataDirectory;

    public AppPaths()
    {
        var overridePath = Environment.GetEnvironmentVariable(AppConstants.DataDirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            _dataDirectory = Path.GetFullPath(overridePath);
        }
        else
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dataDirectory = Path.Combine(local, AppConstants.AppName);
        }
    }

    public string DataDirectory => _dataDirectory;

    public string DatabasePath => Path.Combine(_dataDirectory, AppConstants.DatabaseFileName);

    public string ThumbnailDirectory => Path.Combine(_dataDirectory, AppConstants.ThumbnailCacheDirectoryName);

    public string LogDirectory => Path.Combine(_dataDirectory, AppConstants.LogsDirectoryName);

    public string TempDirectory => Path.Combine(_dataDirectory, AppConstants.TempDirectoryName);

    public string ConfigFilePath => Path.Combine(_dataDirectory, AppConstants.ConfigFileName);

    public string SecretsFilePath => Path.Combine(_dataDirectory, AppConstants.SecretsFileName);

    public string ModelsDirectory => Path.Combine(_dataDirectory, AppConstants.ModelsDirectoryName);

    public string ClipModelDirectory => Path.Combine(ModelsDirectory, AppConstants.ClipModelDirectoryName);

    public string ClipTextCacheFilePath => Path.Combine(_dataDirectory, AppConstants.ClipTextCacheFileName);

    public void EnsureCreated()
    {
        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(ThumbnailDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(ClipModelDirectory);
    }
}
