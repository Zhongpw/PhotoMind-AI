using PhotoMind.Core.Constants;
using PhotoMind.Core.Interfaces;
using Serilog;
using Serilog.Events;

namespace PhotoMind.Infrastructure.Logging;

/// <summary>Configures the Serilog file logger. API keys / Base64 are never logged by policy.</summary>
public static class LoggingConfig
{
    public static ILogger Configure(IAppPaths paths, bool verbose = false)
    {
        var minimum = verbose ? LogEventLevel.Debug : LogEventLevel.Information;

        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimum)
            .WriteTo.File(
                Path.Combine(paths.LogDirectory, AppConstants.LogFileName),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Logger = logger;
        return logger;
    }
}
