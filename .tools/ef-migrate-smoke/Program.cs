using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.Constants;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;

var dataDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "ef-migrate-smoke");
if (Directory.Exists(dataDir))
{
    Directory.Delete(dataDir, recursive: true);
}
Directory.CreateDirectory(dataDir);
Environment.SetEnvironmentVariable(AppConstants.DataDirEnvironmentVariable, dataDir);

var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
using var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

// First run: applies migration + seeds.
await DatabaseInitializer.InitializeAsync(factory);

await using (var db = await factory.CreateDbContextAsync())
{
    var history = await db.Database.SqlQueryRaw<string>("SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId").ToListAsync();
    Console.WriteLine($"Migrations applied: {history.Count}");
    foreach (var m in history)
    {
        Console.WriteLine($"  - {m}");
    }

    var categories = await db.Categories.CountAsync();
    var topLevel = await db.Categories.CountAsync(c => c.ParentId == null);
    Console.WriteLine($"Categories: {categories} (top-level: {topLevel})");
}

// Second run: must be idempotent (no error, no duplicate seed).
await DatabaseInitializer.InitializeAsync(factory);

await using (var db2 = await factory.CreateDbContextAsync())
{
    var history2 = await db2.Database.SqlQueryRaw<string>("SELECT MigrationId FROM __EFMigrationsHistory").ToListAsync();
    var categories2 = await db2.Categories.CountAsync();
    Console.WriteLine($"After re-init: migrations={history2.Count}, categories={categories2}");

    var ok = history2.Count == 1
             && history2[0].Contains("InitialCreate", StringComparison.Ordinal)
             && categories2 >= 6;
    Console.WriteLine(ok ? "VERIFY: PASS" : "VERIFY: FAIL");
    return ok ? 0 : 1;
}
