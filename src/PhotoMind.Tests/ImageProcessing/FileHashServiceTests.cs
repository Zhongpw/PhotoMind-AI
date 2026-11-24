using System.Security.Cryptography;
using System.Text;
using PhotoMind.Infrastructure.ImageProcessing;

namespace PhotoMind.Tests.ImageProcessing;

public class FileHashServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly FileHashService _service = new();

    public FileHashServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "photomind-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task ComputeSha256Async_MatchesReference()
    {
        var path = Path.Combine(_dir, "sample.bin");
        var bytes = Encoding.UTF8.GetBytes("hello photomin");
        await File.WriteAllBytesAsync(path, bytes);

        var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var actual = await _service.ComputeSha256Async(path);

        Assert.Equal(expected, actual);
        Assert.Equal(64, actual.Length);
        Assert.True(actual.All(c => char.IsAsciiHexDigitLower(c)));
    }

    [Fact]
    public async Task ComputeSha256Async_EmptyFile_ReturnsEmptyHash()
    {
        var path = Path.Combine(_dir, "empty.bin");
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());

        var expected = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant();
        var actual = await _service.ComputeSha256Async(path);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ComputeSha256Async_SameContent_SameHash()
    {
        var p1 = Path.Combine(_dir, "a.bin");
        var p2 = Path.Combine(_dir, "b.bin");
        var bytes = new byte[4096];
        new Random(42).NextBytes(bytes);
        await File.WriteAllBytesAsync(p1, bytes);
        await File.WriteAllBytesAsync(p2, bytes);

        Assert.Equal(await _service.ComputeSha256Async(p1), await _service.ComputeSha256Async(p2));
    }
}
