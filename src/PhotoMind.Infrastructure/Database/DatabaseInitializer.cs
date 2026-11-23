using Microsoft.EntityFrameworkCore;

namespace PhotoMind.Infrastructure.Database;

/// <summary>
/// Creates the SQLite schema on first run. Phase 1 uses EnsureCreated; EF Core migrations are introduced
/// in Phase 2 once the model stabilises.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IDbContextFactory<AppDbContext> factory, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }
}
