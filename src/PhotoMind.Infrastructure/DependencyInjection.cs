using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.Interfaces;
using PhotoMind.Infrastructure.AI;
using PhotoMind.Infrastructure.Database;
using PhotoMind.Infrastructure.FileSystem;
using PhotoMind.Infrastructure.ImageProcessing;
using PhotoMind.Infrastructure.Security;
using PhotoMind.Infrastructure.Services;
using Serilog;

namespace PhotoMind.Infrastructure;

/// <summary>Registers all Infrastructure services. Stateless services are singletons; the DbContext uses a factory.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPhotoMindInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IImageFormatDetector, ImageFormatDetector>();
        services.AddSingleton<IFileHashService, FileHashService>();
        services.AddSingleton<IImageMetadataReader, ImageMetadataReader>();
        services.AddSingleton<IThumbnailService, ThumbnailService>();
        services.AddSingleton<DirectoryScanner>();
        services.AddSingleton<IScanRepository, ScanRepository>();
        services.AddSingleton<IScanService, ScanService>();

        // AI / classification services.
        services.AddSingleton<ILogger>(_ => Log.Logger);
        services.AddSingleton<ISecretStore, SecretStore>();
        services.AddSingleton<IAiConfigStore, AiConfigStore>();
        services.AddSingleton<IImageNormalizer, ImageNormalizer>();
        services.AddSingleton<IDeepSeekClient, DeepSeekClient>();
        services.AddSingleton<AIRequestManager>();
        services.AddSingleton<IClassificationRepository, ClassificationRepository>();
        services.AddSingleton<IClassificationService, ClassificationService>();

        // Organize (file arrangement) services.
        services.AddSingleton<SafeFileOperations>();
        services.AddSingleton<IOrganizeRepository, OrganizeRepository>();
        services.AddSingleton<IOrganizeService, OrganizeService>();

        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            var paths = sp.GetRequiredService<IAppPaths>();
            options.UseSqlite($"Data Source={paths.DatabasePath}");
        });

        return services;
    }
}
