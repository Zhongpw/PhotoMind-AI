using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.Interfaces;
using PhotoMind.Infrastructure.Database;
using PhotoMind.Infrastructure.FileSystem;
using PhotoMind.Infrastructure.ImageProcessing;
using PhotoMind.Infrastructure.Services;

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

        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            var paths = sp.GetRequiredService<IAppPaths>();
            options.UseSqlite($"Data Source={paths.DatabasePath}");
        });

        return services;
    }
}
