using Microsoft.EntityFrameworkCore;
using PhotoMind.Core.Models;

namespace PhotoMind.Infrastructure.Database;

/// <summary>EF Core model for the local SQLite database.</summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Image> Images => Set<Image>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ClassificationResult> ClassificationResults => Set<ClassificationResult>();
    public DbSet<ScanTask> ScanTasks => Set<ScanTask>();
    public DbSet<ClassificationTask> ClassificationTasks => Set<ClassificationTask>();
    public DbSet<OrganizeTask> OrganizeTasks => Set<OrganizeTask>();
    public DbSet<OrganizeLog> OrganizeLogs => Set<OrganizeLog>();
    public DbSet<UserRule> UserRules => Set<UserRule>();
    public DbSet<ApiUsage> ApiUsages => Set<ApiUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Image>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(512).IsRequired();
            e.Property(x => x.OriginalPath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.FileHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.ThumbnailPath).HasMaxLength(1024);
            e.HasIndex(x => x.OriginalPath);
            e.HasIndex(x => x.FileHash);
            e.HasIndex(x => x.ScanTaskId);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1024);
            e.HasIndex(x => x.ParentId);
        });

        modelBuilder.Entity<ClassificationResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Description).HasMaxLength(4096);
            e.Property(x => x.Reason).HasMaxLength(4096);
            e.Property(x => x.TagsJson).HasMaxLength(4096);
            e.Property(x => x.ModelName).HasMaxLength(128);
            e.Property(x => x.PromptVersion).HasMaxLength(64);
            e.HasIndex(x => x.ImageId);
            e.HasIndex(x => x.CategoryId);
        });

        modelBuilder.Entity<ScanTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SourcePath).HasMaxLength(8192);
            e.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<ClassificationTask>(e => e.HasKey(x => x.Id));

        modelBuilder.Entity<OrganizeTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SourcePath).HasMaxLength(1024);
            e.Property(x => x.DestinationPath).HasMaxLength(1024);
        });

        modelBuilder.Entity<OrganizeLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SourcePath).HasMaxLength(1024);
            e.Property(x => x.DestinationPath).HasMaxLength(1024);
            e.Property(x => x.ErrorMessage).HasMaxLength(2048);
            e.HasIndex(x => x.TaskId);
            e.HasIndex(x => x.ImageId);
        });

        modelBuilder.Entity<UserRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RuleName).HasMaxLength(256).IsRequired();
            e.Property(x => x.RuleContent).HasMaxLength(8192);
        });

        modelBuilder.Entity<ApiUsage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ModelName).HasMaxLength(128);
            e.Property(x => x.RequestStatus).HasMaxLength(128);
            e.HasIndex(x => x.TaskId);
            e.HasIndex(x => x.CreatedAt);
        });
    }
}
