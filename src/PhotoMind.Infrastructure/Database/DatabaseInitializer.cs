using Microsoft.EntityFrameworkCore;

namespace PhotoMind.Infrastructure.Database;

/// <summary>
/// Applies pending EF Core migrations on startup and seeds the built-in category tree.
/// Migrations give schema versioning (upgrade path for future releases); the seed only runs
/// when the category table is empty, so re-runs are idempotent.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IDbContextFactory<AppDbContext> factory, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await CategorySeed.SeedAsync(db, cancellationToken).ConfigureAwait(false);
    }
}
