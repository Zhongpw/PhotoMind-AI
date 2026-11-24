using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PhotoMind.Infrastructure.Services;

namespace PhotoMind.Infrastructure.Database;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> and EF Core migrations tooling.
/// It reuses <see cref="AppPaths"/> so the migration connection string matches the app's runtime database path.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var paths = new AppPaths();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={paths.DatabasePath}")
            .Options;

        return new AppDbContext(options);
    }
}
