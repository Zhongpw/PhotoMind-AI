using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.Security;

/// <summary>
/// Stores small secrets (the DeepSeek API key) encrypted with Windows DPAPI (CryptProtectData / CryptUnprotectData).
/// The encrypted blob is persisted to a local file; the key never appears in logs, config JSON or source.
/// </summary>
public sealed class SecretStore : ISecretStore
{
    private readonly string _filePath;

    public SecretStore(IAppPaths paths) => _filePath = paths.SecretsFilePath;

    public Task SaveSecretAsync(string key, string value, CancellationToken ct = default)
    {
        var dict = LoadDictionary();
        dict[key] = Protect(Encoding.UTF8.GetBytes(value));
        Persist(dict);
        return Task.CompletedTask;
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        var dict = LoadDictionary();
        if (!dict.TryGetValue(key, out var encrypted))
            return Task.FromResult<string?>(null);
        try
        {
            return Task.FromResult<string?>(Encoding.UTF8.GetString(Unprotect(encrypted)));
        }
        catch (CryptographicException)
        {
            return Task.FromResult<string?>(null);
        }
        catch (FormatException)
        {
            // The stored value is not valid base64 (corrupt secrets file). Treat as "cannot decrypt".
            return Task.FromResult<string?>(null);
        }
    }

    public Task DeleteSecretAsync(string key, CancellationToken ct = default)
    {
        var dict = LoadDictionary();
        if (dict.Remove(key))
            Persist(dict);
        return Task.CompletedTask;
    }

    public Task<bool> HasSecretAsync(string key, CancellationToken ct = default)
        => Task.FromResult(LoadDictionary().ContainsKey(key));

    private Dictionary<string, string> LoadDictionary()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, string>();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private void Persist(Dictionary<string, string> dict)
    {
        var json = JsonSerializer.Serialize(dict);
        File.WriteAllText(_filePath, json);
    }

    private static string Protect(byte[] plaintext)
    {
        var inBlob = new DataBlob { Size = plaintext.Length, Data = Marshal.AllocHGlobal(plaintext.Length) };
        try
        {
            Marshal.Copy(plaintext, 0, inBlob.Data, plaintext.Length);
            if (!CryptProtectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var outBlob))
                throw new CryptographicException(Marshal.GetLastWin32Error());
            try
            {
                var result = new byte[outBlob.Size];
                Marshal.Copy(outBlob.Data, result, 0, outBlob.Size);
                return Convert.ToBase64String(result);
            }
            finally
            {
                LocalFree(outBlob.Data);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inBlob.Data);
        }
    }

    private static byte[] Unprotect(string base64)
    {
        var cipher = Convert.FromBase64String(base64);
        var inBlob = new DataBlob { Size = cipher.Length, Data = Marshal.AllocHGlobal(cipher.Length) };
        try
        {
            Marshal.Copy(cipher, 0, inBlob.Data, cipher.Length);
            if (!CryptUnprotectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var outBlob))
                throw new CryptographicException(Marshal.GetLastWin32Error());
            try
            {
                var result = new byte[outBlob.Size];
                Marshal.Copy(outBlob.Data, result, 0, outBlob.Size);
                return result;
            }
            finally
            {
                LocalFree(outBlob.Data);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inBlob.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
