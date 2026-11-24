using System.Diagnostics;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhotoMind.App.ViewModels;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;

var repo = args.Length > 0 ? args[0] : "D:\\AI_Agent+\\dsh_next\\PhotoMindAI";
var dataDir = Path.Combine(repo, ".testdata-api-status");

if (Directory.Exists(dataDir))
    Directory.Delete(dataDir, true);

int pass = 0, fail = 0;

void Check(bool cond, string name)
{
    if (cond) { pass++; Console.WriteLine($"PASS: {name}"); }
    else { fail++; Console.WriteLine($"FAIL: {name}"); }
}

var fakeClient = new FakeDeepSeekClient();

ServiceProvider BuildProvider(string dir)
{
    Environment.SetEnvironmentVariable("PHOTOMIND_DATA_DIR", dir);
    var services = new ServiceCollection();
    services.AddPhotoMindInfrastructure();
    services.RemoveAll<IDeepSeekClient>();
    services.AddSingleton<IDeepSeekClient>(fakeClient);
    services.AddSingleton<MainViewModel>();
    services.AddSingleton<HomeViewModel>();
    services.AddSingleton<ScanViewModel>();
    services.AddSingleton<ClassificationViewModel>();
    services.AddSingleton<ReviewViewModel>();
    services.AddSingleton<OrganizeViewModel>();
    services.AddSingleton<CategoryManagementViewModel>();
    services.AddSingleton<HistoryViewModel>();
    services.AddSingleton<SettingsViewModel>();
    var provider = services.BuildServiceProvider();

    var paths = provider.GetRequiredService<IAppPaths>();
    paths.EnsureCreated();

    var dbFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    DatabaseInitializer.InitializeAsync(dbFactory).GetAwaiter().GetResult();
    return provider;
}

async Task WaitForAsync(MainViewModel vm, string text, int ms = 3000)
{
    var sw = Stopwatch.StartNew();
    while (sw.ElapsedMilliseconds < ms && vm.ApiStatusText != text)
        await Task.Delay(30);
}

// ---- Scenario 1/2/5/6/7: same session state transitions ----
using (var provider = BuildProvider(dataDir))
{
    var main = provider.GetRequiredService<MainViewModel>();
    var settings = provider.GetRequiredService<SettingsViewModel>();

    await WaitForAsync(main, "未配置");
    Check(main.ApiStatusText == "未配置" && main.ApiStatus == ApiStatusKind.NotConfigured,
        "1. 首次启动无密钥 -> 未配置");

    settings.ApiKeyInput = "sk-test-123";
    await settings.SaveApiKeyCommand.ExecuteAsync(null);
    Check(main.ApiStatusText == "已配置" && main.ApiStatus == ApiStatusKind.Configured,
        "2. 保存密钥后立即 -> 已配置");

    fakeClient.Throw = false;
    await settings.TestConnectionCommand.ExecuteAsync(null);
    Check(main.ApiStatusText == "已连接" && main.ApiStatus == ApiStatusKind.Connected,
        "5. 测试成功 -> 已连接");

    fakeClient.Throw = true;
    await settings.TestConnectionCommand.ExecuteAsync(null);
    Check(main.ApiStatusText == "连接失败" && main.ApiStatus == ApiStatusKind.Failed,
        "6. 测试失败 -> 连接失败");

    await settings.DeleteApiKeyCommand.ExecuteAsync(null);
    Check(main.ApiStatusText == "未配置" && main.ApiStatus == ApiStatusKind.NotConfigured,
        "7. 删除密钥后 -> 未配置");
}

// ---- Scenario 3: restart (persisted key read by a fresh container on the same data dir) ----
{
    using (var p = BuildProvider(dataDir))
    {
        var s = p.GetRequiredService<SettingsViewModel>();
        s.ApiKeyInput = "sk-persisted";
        await s.SaveApiKeyCommand.ExecuteAsync(null);
    }

    using var provider2 = BuildProvider(dataDir);
    var main2 = provider2.GetRequiredService<MainViewModel>();
    await WaitForAsync(main2, "已配置");
    Check(main2.ApiStatusText == "已配置" && main2.ApiStatus == ApiStatusKind.Configured,
        "3. 重启后 -> 已配置");
}

// ---- Scenario 4: decrypt failure -> 密钥异常 ----
{
    // Corrupt the secrets file: valid JSON with a value that is valid base64 but NOT a valid DPAPI blob,
    // so HasSecretAsync reports the entry exists while GetSecretAsync fails to decrypt and returns null.
    var secretsFile = Path.Combine(dataDir, "secrets.dat");
    File.WriteAllText(secretsFile, "{\"deepseek-api-key\":\"dGhpc2lzbm90YWRwYXBpYmxvYg==\"}");

    using var provider3 = BuildProvider(dataDir);
    var main3 = provider3.GetRequiredService<MainViewModel>();
    await WaitForAsync(main3, "密钥异常");
    Check(main3.ApiStatusText == "密钥异常" && main3.ApiStatus == ApiStatusKind.Error,
        "4. 密钥无法解密 -> 密钥异常");
}

Console.WriteLine($"\nRESULT: {pass} pass, {fail} fail");
return fail == 0 ? 0 : 1;

sealed class FakeDeepSeekClient : IDeepSeekClient
{
    public bool Throw { get; set; }

    public Task<DeepSeekResponse> ChatAsync(string baseUrl, string apiKey, DeepSeekRequest request, CancellationToken ct = default)
    {
        if (Throw)
            throw new DeepSeekException(DeepSeekErrorKind.Authentication, "fake 401", 401);
        return Task.FromResult(new DeepSeekResponse());
    }
}
