using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.App.ViewModels;
using PhotoMind.App.Views;
using PhotoMind.Core.Interfaces;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;
using PhotoMind.Infrastructure.Logging;
using Serilog;

namespace PhotoMind.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            var paths = Services.GetRequiredService<IAppPaths>();
            paths.EnsureCreated();

            LoggingConfig.Configure(paths);
            Log.Information("PhotoMind AI 启动");

            var dbFactory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            DatabaseInitializer.InitializeAsync(dbFactory).GetAwaiter().GetResult();

            // Recover tasks left in a running state by an abnormal exit (never touches originals).
            var recovery = Services.GetRequiredService<ITaskRecoveryService>();
            var recoveryResult = recovery.RecoverInterruptedTasksAsync().GetAwaiter().GetResult();
            if (recoveryResult.TotalRecovered > 0)
                Log.Warning("任务恢复：{Count} 个中断任务已标记为失败", recoveryResult.TotalRecovered);

            var window = Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "启动失败");
            MessageBox.Show("程序启动失败：\n\n" + ex.Message, "PhotoMind AI", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddPhotoMindInfrastructure();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<ScanViewModel>();
        services.AddSingleton<ClassificationViewModel>();
        services.AddSingleton<ReviewViewModel>();
        services.AddSingleton<OrganizeViewModel>();
        services.AddSingleton<CategoryManagementViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();

        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("PhotoMind AI 退出 (code {Code})", e.ApplicationExitCode);
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "未处理的 UI 异常");
        e.Handled = true;
        MessageBox.Show("发生了一个未预期的错误：\n\n" + e.Exception.Message, "PhotoMind AI", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Error(ex, "未处理的线程异常");
    }
}
