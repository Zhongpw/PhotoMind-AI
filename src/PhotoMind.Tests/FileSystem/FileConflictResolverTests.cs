using PhotoMind.Infrastructure.FileSystem;

namespace PhotoMind.Tests.FileSystem;

public class FileConflictResolverTests : IDisposable
{
    private readonly string _dir;

    public FileConflictResolverTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "photomind-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void GetUniquePath_NoConflict_ReturnsOriginal()
    {
        var target = Path.Combine(_dir, "photo.jpg");
        Assert.Equal(target, FileConflictResolver.GetUniquePath(target));
    }

    [Fact]
    public void GetUniquePath_ExistingFile_AddsSuffix()
    {
        var target = Path.Combine(_dir, "photo.jpg");
        File.WriteAllText(target, "x");
        Assert.Equal(Path.Combine(_dir, "photo_1.jpg"), FileConflictResolver.GetUniquePath(target));
    }

    [Fact]
    public void GetUniquePath_SuffixAlsoExists_Increments()
    {
        var target = Path.Combine(_dir, "photo.jpg");
        File.WriteAllText(target, "x");
        File.WriteAllText(Path.Combine(_dir, "photo_1.jpg"), "x");
        Assert.Equal(Path.Combine(_dir, "photo_2.jpg"), FileConflictResolver.GetUniquePath(target));
    }

    [Fact]
    public void GetUniquePath_NoExtension_Handled()
    {
        var target = Path.Combine(_dir, "photo");
        File.WriteAllText(target, "x");
        Assert.Equal(Path.Combine(_dir, "photo_1"), FileConflictResolver.GetUniquePath(target));
    }
}
