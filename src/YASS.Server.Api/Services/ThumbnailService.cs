using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using YASS.Shared.Interfaces;

namespace YASS.Server.Api.Services;

/// <summary>
/// 缩略图服务 - 使用 FFmpeg 从直播流定期截图
/// </summary>
public class ThumbnailService : IDisposable
{
    private readonly IRoomService _roomService;
    private readonly SrsSettings _srsSettings;
    private readonly ILogger<ThumbnailService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _captureTasks = new();
    private readonly string _thumbnailPath;

    public ThumbnailService(
        IRoomService roomService,
        IOptions<SrsSettings> srsSettings,
        ILogger<ThumbnailService> logger)
    {
        _roomService = roomService;
        _srsSettings = srsSettings.Value;
        _logger = logger;

        // 确保缩略图目录存在
        _thumbnailPath = Path.Combine(Directory.GetCurrentDirectory(), _srsSettings.ThumbnailStoragePath);
        if (!Directory.Exists(_thumbnailPath))
        {
            Directory.CreateDirectory(_thumbnailPath);
            _logger.LogInformation("Created thumbnail directory: {Path}", _thumbnailPath);
        }
    }

    /// <summary>
    /// 启动房间的定期截图任务
    /// </summary>
    public void StartCapture(string roomId)
    {
        if (_captureTasks.ContainsKey(roomId))
        {
            _logger.LogWarning("Capture task already running for room {RoomId}", roomId);
            return;
        }

        var cts = new CancellationTokenSource();
        if (_captureTasks.TryAdd(roomId, cts))
        {
            _logger.LogInformation("Starting thumbnail capture for room {RoomId}", roomId);
            _ = CaptureLoopAsync(roomId, cts.Token);
        }
    }

    /// <summary>
    /// 停止房间的截图任务
    /// </summary>
    public void StopCapture(string roomId)
    {
        if (_captureTasks.TryRemove(roomId, out var cts))
        {
            _logger.LogInformation("Stopping thumbnail capture for room {RoomId}", roomId);
            cts.Cancel();
            cts.Dispose();

            // 删除缩略图文件
            var thumbnailFile = GetThumbnailFilePath(roomId);
            if (File.Exists(thumbnailFile))
            {
                try
                {
                    File.Delete(thumbnailFile);
                    _logger.LogDebug("Deleted thumbnail file for room {RoomId}", roomId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete thumbnail file for room {RoomId}", roomId);
                }
            }

            // 清除数据库中的缩略图URL
            _ = _roomService.UpdateThumbnailAsync(roomId, null);
        }
    }

    /// <summary>
    /// 检查房间是否正在截图
    /// </summary>
    public bool IsCapturing(string roomId)
    {
        return _captureTasks.ContainsKey(roomId);
    }

    private async Task CaptureLoopAsync(string roomId, CancellationToken cancellationToken)
    {
        // 等待一小段时间让流稳定
        await Task.Delay(3000, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CaptureFrameAsync(roomId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error capturing thumbnail for room {RoomId}", roomId);
            }

            try
            {
                await Task.Delay(_srsSettings.ThumbnailIntervalSeconds * 1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogDebug("Capture loop ended for room {RoomId}", roomId);
    }

    private async Task CaptureFrameAsync(string roomId, CancellationToken cancellationToken)
    {
        var httpFlvUrl = $"{_srsSettings.HttpFlvServer}/{_srsSettings.AppName}/{roomId}.flv";
        var outputPath = GetThumbnailFilePath(roomId);
        var tempPath = outputPath + ".tmp";

        // FFmpeg 命令：从 HTTP-FLV 流截取一帧
        var arguments = $"-y -i \"{httpFlvUrl}\" -vframes 1 -q:v 2 -f image2 \"{tempPath}\"";

        _logger.LogDebug("Capturing thumbnail: ffmpeg {Arguments}", arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = _srsSettings.FFmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        try
        {
            process.Start();

            // 设置超时（10秒）
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(10000);

            await process.WaitForExitAsync(timeoutCts.Token);

            if (process.ExitCode == 0 && File.Exists(tempPath))
            {
                // 原子性替换文件
                File.Move(tempPath, outputPath, overwrite: true);

                // 更新数据库中的缩略图URL
                var thumbnailUrl = $"/thumbnails/{roomId}.jpg";
                await _roomService.UpdateThumbnailAsync(roomId, thumbnailUrl);

                _logger.LogDebug("Thumbnail captured successfully for room {RoomId}", roomId);
            }
            else
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning("FFmpeg failed for room {RoomId}: {Error}", roomId, stderr);
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private string GetThumbnailFilePath(string roomId)
    {
        return Path.Combine(_thumbnailPath, $"{roomId}.jpg");
    }

    public void Dispose()
    {
        foreach (var kvp in _captureTasks)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        _captureTasks.Clear();
    }
}
