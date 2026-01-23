using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using YASS.Client.Desktop.Services;
using YASS.Shared.Models;

namespace YASS.Client.Desktop.ViewModels;

public partial class StreamingViewModel : ObservableObject, IDisposable
{
    // Win32 API 用于获取真实屏幕分辨率（不受 DPI 缩放影响）
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    
    private bool _disposed;
    private bool _shouldAutoRestart = false;
    
    private const int SM_CXSCREEN = 0;  // 主屏幕宽度
    private const int SM_CYSCREEN = 1;  // 主屏幕高度

    private readonly ILogger<StreamingViewModel> _logger;
    private readonly IApiService _apiService;
    private RoomInfo? _room;
    private Process? _ffmpegProcess;

    [ObservableProperty]
    private string _statusMessage = "准备就绪";

    [ObservableProperty]
    private string _roomName = string.Empty;

    [ObservableProperty]
    private string _rtmpUrl = string.Empty;

    [ObservableProperty]
    private string _streamKey = string.Empty;

    [ObservableProperty]
    private bool _isRtmpUrlReady;

    [ObservableProperty]
    private bool _isFFmpegStreaming;

    [ObservableProperty]
    private string _selectedCaptureMethod = "gdigrab";

    [ObservableProperty]
    private string _selectedAudioSource = "静音";

    [ObservableProperty]
    private string[] _audioSourceOptions = new[] { "静音", "立体声混音 (Stereo Mix)" };

    [ObservableProperty]
    private string _selectedOutputResolution = "原始分辨率";

    [ObservableProperty]
    private int _customWidth = 1920;

    [ObservableProperty]
    private int _customHeight = 1080;

    [ObservableProperty]
    private bool _isCustomResolution;

    public string[] CaptureMethodOptions { get; } = new[] { "gdigrab", "dshow" };

    public string[] OutputResolutionOptions { get; } = new[]
    {
        "原始分辨率",
        "4K (3840x2160)",
        "2K (2560x1440)",
        "1080p (1920x1080)",
        "720p (1280x720)",
        "480p (854x480)",
        "自定义"
    };

    partial void OnSelectedOutputResolutionChanged(string value)
    {
        IsCustomResolution = value == "自定义";
    }

    public StreamingViewModel(
        ILogger<StreamingViewModel> logger,
        IApiService apiService)
    {
        _logger = logger;
        _apiService = apiService;
        
        // 初始化时检测可用的音频设备
        _ = DetectAudioDevicesAsync();
    }

    /// <summary>
    /// 检测可用的音频设备
    /// </summary>
    private async Task DetectAudioDevicesAsync()
    {
        try
        {
            var ffmpegPath = GetFFmpegPath();
            var audioDevices = new List<string> { "静音" };

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-list_devices true -f dshow -i dummy",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // 解析音频设备列表
            var lines = stderr.Split('\n');
            bool inAudioSection = false;
            foreach (var line in lines)
            {
                if (line.Contains("DirectShow audio devices"))
                {
                    inAudioSection = true;
                    continue;
                }
                if (inAudioSection && line.Contains("DirectShow video devices"))
                {
                    break;
                }
                if (inAudioSection && line.Contains("\"") && !line.Contains("Alternative name"))
                {
                    // 提取设备名称
                    var match = System.Text.RegularExpressions.Regex.Match(line, "\"([^\"]+)\"");
                    if (match.Success)
                    {
                        var deviceName = match.Groups[1].Value;
                        audioDevices.Add(deviceName);
                        _logger.LogInformation("Detected audio device: {DeviceName}", deviceName);
                    }
                }
            }

            AudioSourceOptions = audioDevices.ToArray();
            _logger.LogInformation("Detected {Count} audio devices", audioDevices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect audio devices");
            AudioSourceOptions = new[] { "静音" };
        }
    }

    public void Initialize(RoomInfo room)
    {
        _room = room;
        RoomName = room.Name;
        
        // 立即获取RTMP推流地址
        _ = LoadRtmpUrlAsync();
    }

    [RelayCommand]
    private async Task LoadRtmpUrlAsync()
    {
        if (_room == null)
        {
            StatusMessage = "房间信息无效";
            return;
        }

        try
        {
            StatusMessage = "正在获取推流地址...";

            // 获取推流凭证
            var credentials = await _apiService.GetPublishCredentialsAsync(_room.Id);
            if (credentials == null)
            {
                StatusMessage = "获取推流凭证失败";
                return;
            }

            // 解析RTMP地址和流密钥
            var fullUrl = credentials.FullPublishUrl;
            var lastSlashIndex = fullUrl.LastIndexOf('/');
            if (lastSlashIndex > 0)
            {
                RtmpUrl = fullUrl.Substring(0, lastSlashIndex);
                StreamKey = fullUrl.Substring(lastSlashIndex + 1);
            }
            else
            {
                RtmpUrl = fullUrl;
                StreamKey = "";
            }

            IsRtmpUrlReady = true;
            StatusMessage = "推流地址已生成，请使用OBS、FFmpeg等工具推流";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load RTMP URL");
            StatusMessage = $"获取推流地址失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyRtmpUrl()
    {
        if (string.IsNullOrEmpty(RtmpUrl))
        {
            StatusMessage = "推流地址为空";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(RtmpUrl);
            StatusMessage = "推流地址已复制到剪贴板";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy RTMP URL");
            StatusMessage = "复制失败";
        }
    }

    [RelayCommand]
    private void CopyStreamKey()
    {
        if (string.IsNullOrEmpty(StreamKey))
        {
            StatusMessage = "流密钥为空";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(StreamKey);
            StatusMessage = "流密钥已复制到剪贴板";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy stream key");
            StatusMessage = "复制失败";
        }
    }

    [RelayCommand]
    private void CopyFullUrl()
    {
        if (string.IsNullOrEmpty(RtmpUrl) || string.IsNullOrEmpty(StreamKey))
        {
            StatusMessage = "推流信息不完整";
            return;
        }

        try
        {
            var fullUrl = $"{RtmpUrl}/{StreamKey}";
            System.Windows.Clipboard.SetText(fullUrl);
            StatusMessage = "完整推流地址已复制到剪贴板";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy full URL");
            StatusMessage = "复制失败";
        }
    }

    /// <summary>
    /// 获取FFmpeg可执行文件路径
    /// </summary>
    private string GetFFmpegPath()
    {
        // 优先查找应用程序目录下的ffmpeg
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var ffmpegInAppDir = Path.Combine(appDir, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(ffmpegInAppDir))
        {
            return ffmpegInAppDir;
        }

        // 然后查找tools子目录
        var ffmpegInTools = Path.Combine(appDir, "tools", "ffmpeg.exe");
        if (File.Exists(ffmpegInTools))
        {
            return ffmpegInTools;
        }

        // 最后尝试使用系统PATH中的ffmpeg
        return "ffmpeg";
    }

    /// <summary>
    /// 检查FFmpeg是否可用
    /// </summary>
    private bool IsFFmpegAvailable()
    {
        try
        {
            var ffmpegPath = GetFFmpegPath();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取输出分辨率的缩放滤镜
    /// </summary>
    private string GetScaleFilter(int sourceWidth, int sourceHeight)
    {
        int targetWidth, targetHeight;

        switch (SelectedOutputResolution)
        {
            case "原始分辨率":
                return string.Empty;  // 不需要缩放
            case "4K (3840x2160)":
                targetWidth = 3840;
                targetHeight = 2160;
                break;
            case "2K (2560x1440)":
                targetWidth = 2560;
                targetHeight = 1440;
                break;
            case "1080p (1920x1080)":
                targetWidth = 1920;
                targetHeight = 1080;
                break;
            case "720p (1280x720)":
                targetWidth = 1280;
                targetHeight = 720;
                break;
            case "480p (854x480)":
                targetWidth = 854;
                targetHeight = 480;
                break;
            case "自定义":
                targetWidth = CustomWidth;
                targetHeight = CustomHeight;
                break;
            default:
                return string.Empty;
        }

        // 如果目标分辨率与源相同，则不需要缩放
        if (targetWidth == sourceWidth && targetHeight == sourceHeight)
        {
            return string.Empty;
        }

        _logger.LogInformation("Scaling from {SourceWidth}x{SourceHeight} to {TargetWidth}x{TargetHeight}",
            sourceWidth, sourceHeight, targetWidth, targetHeight);

        // 使用 lanczos 算法进行高质量缩放，确保宽高为偶数（x264要求）
        return $"scale={targetWidth}:{targetHeight}:flags=lanczos,format=yuv420p";
    }

    [RelayCommand]
    private async Task StartFFmpegStreamingAsync()
    {
        if (IsFFmpegStreaming)
        {
            StatusMessage = "FFmpeg推流已在运行中";
            return;
        }

        if (string.IsNullOrEmpty(RtmpUrl) || string.IsNullOrEmpty(StreamKey))
        {
            StatusMessage = "推流地址不完整，请先获取推流地址";
            return;
        }

        // 检查FFmpeg是否可用
        if (!IsFFmpegAvailable())
        {
            StatusMessage = "未找到FFmpeg，请将ffmpeg.exe放入应用程序的ffmpeg或tools文件夹";
            _logger.LogWarning("FFmpeg not found");
            return;
        }

        try
        {
            var ffmpegPath = GetFFmpegPath();
            var fullUrl = $"{RtmpUrl}/{StreamKey}";

            // 获取主显示器物理分辨率（使用 Win32 API，不受 DPI 缩放影响）
            var screenWidth = GetSystemMetrics(SM_CXSCREEN);
            var screenHeight = GetSystemMetrics(SM_CYSCREEN);
            
            _logger.LogInformation("Physical screen resolution: {Width}x{Height}", screenWidth, screenHeight);

            // 构建音频输入参数
            string audioInputArgs;
            string audioMapArgs;
            bool useDesktopAudio = SelectedAudioSource != "静音";
            
            if (useDesktopAudio)
            {
                // 使用 DirectShow 捕获选中的音频设备（如立体声混音、麦克风等）
                // 注意：如果选择立体声混音，需要在 Windows 声音设置中启用
                audioInputArgs = $"-f dshow -i audio=\"{SelectedAudioSource}\"";
                audioMapArgs = "-map 1:v -map 0:a";  // 视频来自输入1，音频来自输入0
            }
            else
            {
                // 使用虚拟静音音频源
                audioInputArgs = "-f lavfi -i anullsrc=r=44100:cl=stereo";
                audioMapArgs = "-map 1:v -map 0:a";  // 视频来自输入1，音频来自输入0
            }

            // 根据选择的捕获方法构建视频输入命令
            string videoInputArgs;
            if (SelectedCaptureMethod == "gdigrab")
            {
                // gdigrab 需要明确指定视频大小和帧率
                videoInputArgs = $"-f gdigrab -framerate 30 -offset_x 0 -offset_y 0 -video_size {screenWidth}x{screenHeight} -draw_mouse 1 -i desktop";
            }
            else // dshow
            {
                videoInputArgs = "-f dshow -i video=\"screen-capture-recorder\"";
            }

            // 组合输入参数：音频在前，视频在后
            string inputArgs = $"{audioInputArgs} {videoInputArgs}";

            // 构建缩放滤镜（如果需要）
            string scaleFilter = GetScaleFilter(screenWidth, screenHeight);
            string videoFilterArgs = string.IsNullOrEmpty(scaleFilter) ? "" : $"-vf \"{scaleFilter}\" ";

            // 优化的编码参数：
            // - 音频使用 AAC 编码
            // - profile baseline: 更好的兼容性
            // - level 3.1: 适合网络流
            // - g 30: 每30帧一个关键帧（1秒@30fps），FLV.js需要频繁的关键帧
            // - keyint_min 30: 最小关键帧间隔
            // - sc_threshold 0: 禁用场景切换检测
            // - flvflags no_duration_filesize: 用于直播流
            // - shortest: 当最短的输入结束时停止（视频结束时停止）
            var arguments = $"{inputArgs} " +
                $"{audioMapArgs} " +
                $"{videoFilterArgs}" +
                $"-c:v libx264 " +
                $"-profile:v baseline " +
                $"-level 3.1 " +
                $"-preset ultrafast " +
                $"-tune zerolatency " +
                $"-b:v 2500k " +
                $"-maxrate 2500k " +
                $"-bufsize 2500k " +
                $"-pix_fmt yuv420p " +
                $"-g 30 " +
                $"-keyint_min 30 " +
                $"-sc_threshold 0 " +
                $"-c:a aac " +           // 音频编码为 AAC
                $"-b:a 128k " +          // 音频码率
                $"-ar 44100 " +          // 采样率
                $"-shortest " +          // 当视频结束时停止
                $"-f flv " +
                $"-flvflags no_duration_filesize " +
                $"\"{fullUrl}\"";

            _ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            _ffmpegProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogDebug("FFmpeg: {Message}", e.Data);
                }
            };

            _ffmpegProcess.Exited += async (s, e) =>
            {
                var exitCode = _ffmpegProcess?.ExitCode ?? 0;
                _logger.LogInformation("FFmpeg process exited with code {ExitCode}", exitCode);
                
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    IsFFmpegStreaming = false;
                    
                    if (_shouldAutoRestart && !_disposed)
                    {
                        // Unexpected exit - attempt to restart
                        _logger.LogWarning("FFmpeg process terminated unexpectedly (exit code: {ExitCode}). Attempting to restart...", exitCode);
                        StatusMessage = $"FFmpeg推流意外中断 (退出码: {exitCode})，正在重启...";
                        
                        // Clean up the old process
                        _ffmpegProcess?.Dispose();
                        _ffmpegProcess = null;
                        
                        // Wait a bit before restarting to avoid rapid restart loops
                        await Task.Delay(1000);
                        
                        // Restart if still should be auto-restarting
                        if (_shouldAutoRestart && !_disposed)
                        {
                            _logger.LogInformation("Restarting FFmpeg streaming...");
                            await StartFFmpegStreamingAsync();
                        }
                    }
                    else
                    {
                        // Manual stop or disposal
                        StatusMessage = "FFmpeg推流已停止";
                        _logger.LogInformation("FFmpeg streaming stopped manually");
                    }
                });
            };

            _ffmpegProcess.Start();
            _ffmpegProcess.BeginErrorReadLine();

            _shouldAutoRestart = true;
            IsFFmpegStreaming = true;
            StatusMessage = $"FFmpeg推流已启动 (使用 {SelectedCaptureMethod}, {screenWidth}x{screenHeight})";
            _logger.LogInformation("FFmpeg streaming started with {CaptureMethod}, resolution: {Width}x{Height}", 
                SelectedCaptureMethod, screenWidth, screenHeight);
            _logger.LogInformation("FFmpeg command: {FFmpegPath} {Arguments}", ffmpegPath, arguments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start FFmpeg streaming");
            StatusMessage = $"启动FFmpeg推流失败: {ex.Message}";
            IsFFmpegStreaming = false;
            _shouldAutoRestart = false;
        }
    }

    [RelayCommand]
    private void StopFFmpegStreaming()
    {
        if (_ffmpegProcess == null || _ffmpegProcess.HasExited)
        {
            StatusMessage = "FFmpeg推流未在运行";
            IsFFmpegStreaming = false;
            _shouldAutoRestart = false;
            return;
        }

        try
        {
            // Disable auto-restart before stopping
            _shouldAutoRestart = false;
            
            // 发送q命令让ffmpeg优雅退出
            _ffmpegProcess.Kill();
            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
            IsFFmpegStreaming = false;
            StatusMessage = "FFmpeg推流已停止";
            _logger.LogInformation("FFmpeg streaming stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop FFmpeg streaming");
            StatusMessage = $"停止FFmpeg推流失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 强制终止 FFmpeg 进程（用于应用程序退出时）
    /// </summary>
    public void ForceStopFFmpeg()
    {
        try
        {
            // Disable auto-restart before force stopping
            _shouldAutoRestart = false;
            
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                _logger.LogInformation("Force stopping FFmpeg process...");
                _ffmpegProcess.Kill(true);  // 强制终止进程及其子进程
                _ffmpegProcess.WaitForExit(3000);  // 等待最多3秒
                _ffmpegProcess.Dispose();
                _ffmpegProcess = null;
                _logger.LogInformation("FFmpeg process force stopped");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while force stopping FFmpeg");
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            ForceStopFFmpeg();
        }

        _disposed = true;
    }

    ~StreamingViewModel()
    {
        Dispose(false);
    }
}
