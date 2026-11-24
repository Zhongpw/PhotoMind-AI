using System.IO;
using PhotoMind.Core.Interfaces;
using PhotoMind.Infrastructure.Security;
using Xunit;

namespace PhotoMind.Tests.Security;

public sealed class SecretStoreTests
{
    [Fact]
    public async Task GetSecretAsync_InvalidBase64_ReturnsNullInsteadOfThrowing()
    {
        var file = NewSecretsFile("{\"deepseek-api-key\":\"not-valid-base64!!!\"}");
        try
        {
            var store = new SecretStore(new FakePaths(file));
            var result = await store.GetSecretAsync("deepseek-api-key");
            Assert.Null(result);
        }
        finally
        {
            TryDelete(file);
        }
    }

    [Fact]
    public async Task GetSecretAsync_ValidBase64ButNotDpapiBlob_ReturnsNull()
    {
        // Valid base64 of "thisisnotadpapiblob", which CryptUnprotectData cannot decrypt.
        var file = NewSecretsFile("{\"deepseek-api-key\":\"dGhpc2lzbm90YWRwYXBpYmxvYg==\"}");
        try
        {
            var store = new SecretStore(new FakePaths(file));
            var result = await store.GetSecretAsync("deepseek-api-key");
            Assert.Null(result);
        }
        finally
        {
            TryDelete(file);
        }
    }

    [Fact]
    public async Task HasSecretAsync_ReturnsTrue_WhenEntryExists()
    {
        var file = NewSecretsFile("{\"deepseek-api-key\":\"not-valid-base64!!!\"}");
        try
        {
            var store = new SecretStore(new FakePaths(file));
            Assert.True(await store.HasSecretAsync("deepseek-api-key"));
        }
        finally
        {
            TryDelete(file);
        }
    }

    private static string NewSecretsFile(string content)
    {
        var file = Path.Combine(Path.GetTempPath(), "photomind-secrets-" + Guid.NewGuid().ToString("N") + ".dat");
        File.WriteAllText(file, content);
        return file;
    }

    private static void TryDelete(string file)
    {
        try { if (File.Exists(file)) File.Delete(file); }
        catch (IOException) { }
    }

    private sealed class FakePaths : IAppPaths
    {
        private readonly string _secretsFile;
        public FakePaths(string secretsFile) => _secretsFile = secretsFile;

        public string DataDirectory => _secretsFile;
        public string DatabasePath => _secretsFile;
        public string ThumbnailDirectory => _secretsFile;
        public string LogDirectory => _secretsFile;
        public string TempDirectory => _secretsFile;
        public string ConfigFilePath => _secretsFile;
        public string SecretsFilePath => _secretsFile;

        public void EnsureCreated() { }
    }
}
