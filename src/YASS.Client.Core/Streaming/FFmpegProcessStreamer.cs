using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YASS.Client.Core.Interfaces;
using YASS.Shared.Enums;

namespace YASS.Client.Core.Streaming;

/// <summary>
/// 基于 FFmpeg 进程的 RTMP 推流器
/// 通过管道将编码后的 H.264 数据发送给 ffmpeg 进程进行推流
/// 这是最稳定可靠的方案，避免了非托管内存问题
/// </summary>
public class FFmpegProcessStreamer : IStreamer
{
    private readonly ILogger<FFmpegProcessStreamer> _logger;
    private readonly object _lock = new();

    private StreamerState _state = StreamerState.Disconnected;
    private StreamerOptions? _options;

    // 统计信息
    private long _bytesSent;
    private long _framesSent;
    private long _framesDropped;
    private DateTime _startTime;

    // FFmpeg 进程
    private Process? _ffmpegProcess;
    private Stream? _inputStream;
    private bool _headerWritten;
    private byte[]? _extraData;

    public bool IsConnected => _state == StreamerState.Connected || _state == StreamerState.Streaming;

    public event EventHandler<StreamerStateChangedEventArgs>? StateChanged;
    public event EventHandler<StreamerStatsEventArgs>? StatsUpdated;

    public FFmpegProcessStreamer(ILogger<FFmpegProcessStreamer> logger)
    {
        _logger = logger;
    }

    public Task ConnectAsync(StreamerOptions options)
    {
        if (_state != StreamerState.Disconnected)
        {
            throw new InvalidOperationException($"Cannot connect while in state {_state}");
        }

        _options = options;
        SetState(StreamerState.Connecting);

        try
        {
            _logger.LogInformation("Connecting to RTMP server via FFmpeg process: {Url}", options.Url);
            _logger.LogInformation("Stream config: {Width}x{Height}@{Fps}fps, codec: {Codec}",
                options.Width, options.Height, options.FrameRate, options.CodecType);

            // 构建 FFmpeg 命令参数
            // 输入：从 stdin 读取 Annex-B 格式的 H.264 流
            // 输出：推送到 RTMP 服务器
            var codecName = options.CodecType == CodecType.H265 ? "hevc" : "h264";
            
            var args = $"-y -f {codecName} -i pipe:0 " +
                       $"-c:v copy " +
                       $"-f flv \"{options.Url}\"";

            _logger.LogInformation("FFmpeg arguments: {Args}", args);

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _ffmpegProcess = new Process { StartInfo = startInfo };
            
            // 捕获错误输出用于调试
            _ffmpegProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogDebug("[FFmpeg] {Data}", e.Data);
                }
            };

            _ffmpegProcess.Start();
            _ffmpegProcess.BeginErrorReadLine();

            _inputStream = _ffmpegProcess.StandardInput.BaseStream;
            _headerWritten = false;

            SetState(StreamerState.Connected);
            _startTime = DateTime.UtcNow;
            _bytesSent = 0;
            _framesSent = 0;
            _framesDropped = 0;

            _logger.LogInformation("FFmpeg process started, PID: {Pid}", _ffmpegProcess.Id);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start FFmpeg process");
            Cleanup();
            SetState(StreamerState.Error, ex.Message);
            throw;
        }
    }

    public Task DisconnectAsync()
    {
        if (_state == StreamerState.Disconnected) return Task.CompletedTask;

        _logger.LogInformation("Disconnecting FFmpeg process");

        try
        {
            Cleanup();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
        finally
        {
            SetState(StreamerState.Disconnected);
        }

        return Task.CompletedTask;
    }

    public Task SendPacketAsync(EncodedPacket packet)
    {
        if (!IsConnected || _state == StreamerState.Error)
        {
            return Task.CompletedTask;
        }

        if (_inputStream == null || _ffmpegProcess == null || _ffmpegProcess.HasExited)
        {
            SetState(StreamerState.Error, "FFmpeg process not running");
            return Task.CompletedTask;
        }

        if (_state == StreamerState.Connected)
        {
            SetState(StreamerState.Streaming);
        }

        try
        {
            lock (_lock)
            {
                if (_state == StreamerState.Error || _state == StreamerState.Disconnected)
                {
                    return Task.CompletedTask;
                }

                // 如果是第一个包且有 extradata，先写入 extradata
                if (!_headerWritten && _extraData != null && _extraData.Length > 0)
                {
                    // 将 AVCC 格式的 extradata 转换为 Annex-B 格式并写入
                    WriteExtraDataAsAnnexB();
                    _headerWritten = true;
                }

                // 将 AVCC 格式的 NAL 单元转换为 Annex-B 格式
                // AVCC: [4字节长度][NAL数据]...
                // Annex-B: [00 00 00 01][NAL数据]...
                var annexBData = ConvertAvccToAnnexB(packet.Data, packet.IsKeyFrame);
                
                _inputStream.Write(annexBData, 0, annexBData.Length);
                _inputStream.Flush();

                _bytesSent += packet.Data.Length;
                _framesSent++;

                _logger.LogDebug("Sent packet: size={Size}, pts={Pts}, keyframe={IsKeyFrame}",
                    packet.Data.Length, packet.Pts, packet.IsKeyFrame);
            }

            if (_framesSent % 30 == 0)
            {
                UpdateStats();
            }

            return Task.CompletedTask;
        }
        catch (IOException ex)
        {
            _logger.LogWarning("FFmpeg pipe broken: {Message}", ex.Message);
            SetState(StreamerState.Error, "Connection lost");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending packet");
            _framesDropped++;
            SetState(StreamerState.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 设置编码器的 extradata（SPS/PPS 等）
    /// </summary>
    public void SetExtraData(byte[] extraData)
    {
        _extraData = extraData;
        _logger.LogInformation("Set extradata: {Length} bytes", extraData.Length);
    }

    /// <summary>
    /// 将 AVCC 格式的 extradata (SPS/PPS) 转换为 Annex-B 格式并写入
    /// </summary>
    private void WriteExtraDataAsAnnexB()
    {
        if (_extraData == null || _extraData.Length < 8 || _inputStream == null)
            return;

        try
        {
            // AVCC extradata 格式 (H.264):
            // [0]: version (always 0x01)
            // [1]: profile
            // [2]: compatibility
            // [3]: level
            // [4]: 0xFC | (NAL size length - 1)  通常是 0xFF 表示 4 字节长度
            // [5]: 0xE0 | SPS count
            // For each SPS:
            //   [2 bytes]: SPS length
            //   [n bytes]: SPS data
            // [1 byte]: PPS count
            // For each PPS:
            //   [2 bytes]: PPS length
            //   [n bytes]: PPS data

            var startCode = new byte[] { 0x00, 0x00, 0x00, 0x01 };
            int pos = 5;

            // 读取 SPS
            int spsCount = _extraData[pos] & 0x1F;
            pos++;

            for (int i = 0; i < spsCount && pos + 2 <= _extraData.Length; i++)
            {
                int spsLen = (_extraData[pos] << 8) | _extraData[pos + 1];
                pos += 2;

                if (pos + spsLen <= _extraData.Length)
                {
                    _inputStream.Write(startCode, 0, startCode.Length);
                    _inputStream.Write(_extraData, pos, spsLen);
                    pos += spsLen;
                }
            }

            // 读取 PPS
            if (pos < _extraData.Length)
            {
                int ppsCount = _extraData[pos];
                pos++;

                for (int i = 0; i < ppsCount && pos + 2 <= _extraData.Length; i++)
                {
                    int ppsLen = (_extraData[pos] << 8) | _extraData[pos + 1];
                    pos += 2;

                    if (pos + ppsLen <= _extraData.Length)
                    {
                        _inputStream.Write(startCode, 0, startCode.Length);
                        _inputStream.Write(_extraData, pos, ppsLen);
                        pos += ppsLen;
                    }
                }
            }

            _inputStream.Flush();
            _logger.LogInformation("Wrote SPS/PPS as Annex-B format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write extradata as Annex-B");
        }
    }

    /// <summary>
    /// 将 AVCC 格式的 NAL 数据转换为 Annex-B 格式
    /// </summary>
    private static byte[] ConvertAvccToAnnexB(byte[] avccData, bool isKeyFrame)
    {
        // 预估输出大小（可能略大于输入）
        using var output = new MemoryStream(avccData.Length + 64);
        var startCode = new byte[] { 0x00, 0x00, 0x00, 0x01 };
        
        int pos = 0;
        while (pos + 4 <= avccData.Length)
        {
            // 读取 4 字节长度（大端序）
            int nalLen = (avccData[pos] << 24) | (avccData[pos + 1] << 16) |
                         (avccData[pos + 2] << 8) | avccData[pos + 3];
            pos += 4;

            if (nalLen <= 0 || pos + nalLen > avccData.Length)
            {
                // 长度无效，可能数据本身就是 Annex-B 格式或已损坏
                // 尝试直接返回原始数据
                return avccData;
            }

            // 写入 Annex-B 起始码
            output.Write(startCode, 0, startCode.Length);
            // 写入 NAL 数据
            output.Write(avccData, pos, nalLen);
            pos += nalLen;
        }

        // 如果没有成功解析任何 NAL，返回原始数据
        if (output.Length == 0)
        {
            return avccData;
        }

        return output.ToArray();
    }

    private void UpdateStats()
    {
        var duration = (DateTime.UtcNow - _startTime).TotalSeconds;
        if (duration <= 0) return;

        var stats = new StreamerStatsEventArgs
        {
            BytesSent = _bytesSent,
            CurrentBitrate = (_bytesSent * 8.0 / 1000.0) / duration,
            FramesSent = _framesSent,
            FramesDropped = _framesDropped,
            DurationSeconds = duration,
            CurrentFps = _framesSent / duration
        };

        StatsUpdated?.Invoke(this, stats);
    }

    private void SetState(StreamerState newState, string? error = null)
    {
        var oldState = _state;
        _state = newState;

        StateChanged?.Invoke(this, new StreamerStateChangedEventArgs
        {
            OldState = oldState,
            NewState = newState,
            Error = error
        });
    }

    private void Cleanup()
    {
        try
        {
            _inputStream?.Close();
            _inputStream?.Dispose();
        }
        catch { }

        try
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                // 给 FFmpeg 一点时间优雅退出
                _ffmpegProcess.StandardInput.Close();
                if (!_ffmpegProcess.WaitForExit(3000))
                {
                    _ffmpegProcess.Kill();
                }
            }
            _ffmpegProcess?.Dispose();
        }
        catch { }

        _inputStream = null;
        _ffmpegProcess = null;
        _headerWritten = false;
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
