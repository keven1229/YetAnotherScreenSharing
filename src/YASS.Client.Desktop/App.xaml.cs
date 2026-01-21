using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using YASS.Client.Desktop.Services;
using YASS.Client.Desktop.ViewModels;

namespace YASS.Client.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // 注册进程退出事件处理
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // 日志
                services.AddLogging(builder =>
                {
                    builder.AddDebug();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                // HTTP 客户端 - 从配置读取 API 地址
                var apiBaseAddress = context.Configuration["ApiBaseAddress"] ?? "http://localhost:5000";
                services.AddHttpClient("YassApi", client =>
                {
                    client.BaseAddress = new Uri(apiBaseAddress);
                });

                // 应用服务
                services.AddSingleton<IApiService, ApiService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<StreamingViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
            })
            .Build();

        Services = _host.Services;

        // 启动主窗口
        var mainWindow = Services.GetRequiredService<MainWindow>();
        
        // 初始化 WPF UI 主题 - 监视系统主题变化
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(mainWindow);
        
        mainWindow.Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        CleanupFFmpegProcesses();
        _host?.Dispose();
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        CleanupFFmpegProcesses();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        CleanupFFmpegProcesses();
    }

    /// <summary>
    /// 清理所有可能残留的 FFmpeg 进程
    /// </summary>
    private void CleanupFFmpegProcesses()
    {
        try
        {
            // 获取当前进程 ID
            var currentProcessId = Environment.ProcessId;
            
            // 查找所有 ffmpeg 进程，并终止由本应用启动的进程
            var ffmpegProcesses = Process.GetProcessesByName("ffmpeg");
            foreach (var process in ffmpegProcesses)
            {
                try
                {
                    // 检查进程是否还在运行
                    if (!process.HasExited)
                    {
                        process.Kill(true);  // 强制终止进程及其子进程
                        process.WaitForExit(1000);
                    }
                    process.Dispose();
                }
                catch
                {
                    // 忽略单个进程清理失败
                }
            }
        }
        catch
        {
            // 忽略清理过程中的异常
        }
    }
}

