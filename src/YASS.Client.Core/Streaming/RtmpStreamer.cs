using Microsoft.Extensions.Logging;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Common;
using Sdcb.FFmpeg.Formats;
using Sdcb.FFmpeg.Raw;
using System.Runtime.InteropServices;
using YASS.Client.Core.Interfaces;
using YASS.Shared.Enums;

namespace YASS.Client.Core.Streaming;

/// <summary>
/// RTMP 推流器实现 - 使用 Sdcb.FFmpeg 库
/// </summary>
public class RtmpStreamer : IStreamer
{
    private readonly ILogger<RtmpStreamer> _logger;
    private readonly object _lock = new();

    private StreamerState _state = StreamerState.Disconnected;
    private StreamerOptions? _options;

    // 统计信息
    private long _bytesSent;
    private long _framesSent;
    private long _framesDropped;
    private DateTime _startTime;

    // FFmpeg 资源
    private FormatContext? _formatContext;
    private MediaStream _videoStream;
    private AVRational _codecTimeBase;
    private AVRational _streamTimeBase;
    private bool _headerWritten;
    private bool _videoStreamSet;

    public bool IsConnected => _state == StreamerState.Connected || _state == StreamerState.Streaming;

    public event EventHandler<StreamerStateChangedEventArgs>? StateChanged;
    public event EventHandler<StreamerStatsEventArgs>? StatsUpdated;

    public RtmpStreamer(ILogger<RtmpStreamer> logger)
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
            _logger.LogInformation("Connecting to RTMP server: {Url}", options.Url);
            _logger.LogInformation("Stream config: {Width}x{Height}@{Fps}fps, codec: {Codec}",
                options.Width, options.Height, options.FrameRate, options.CodecType);

            // 创建输出格式上下文 (FLV 格式用于 RTMP)
            _formatContext = FormatContext.AllocOutput(formatName: "flv");

            // 查找对应的编码器 (用于设置流参数)
            var codecId = options.CodecType switch
            {
                CodecType.H264 => AVCodecID.H264,
                CodecType.H265 => AVCodecID.Hevc,
                _ => AVCodecID.H264
            };

            // 创建视频流
            var codec = Codec.FindEncoderById(codecId);
            _videoStream = _formatContext.NewStream(codec);
            _videoStreamSet = true;

            // 设置流参数
            var codecpar = _videoStream.Codecpar!;
            codecpar.CodecType = AVMediaType.Video;
            codecpar.CodecId = codecId;
            codecpar.Width = options.Width;
            codecpar.Height = options.Height;
            codecpar.Format = (int)AVPixelFormat.Yuv420p;

            // 设置时间基准
            _codecTimeBase = new AVRational(1, options.FrameRate);
            _streamTimeBase = new AVRational(1, 1000); // RTMP 通常使用毫秒时间基准
            _videoStream.TimeBase = _streamTimeBase;

            // 打开输出 URL
            _formatContext.Pb = IOContext.OpenWrite(options.Url);

            // 暂时不写入头部，等收到第一个带 extradata 的包时再写入
            _headerWritten = false;

            SetState(StreamerState.Connected);
            _startTime = DateTime.UtcNow;
            _bytesSent = 0;
            _framesSent = 0;
            _framesDropped = 0;

            _logger.LogInformation("Connected to RTMP server successfully");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RTMP server: {Url}", options.Url);
            Cleanup();
            SetState(StreamerState.Error, ex.Message);
            throw;
        }
    }

    public Task DisconnectAsync()
    {
        if (_state == StreamerState.Disconnected) return Task.CompletedTask;

        _logger.LogInformation("Disconnecting from RTMP server");

        try
        {
            lock (_lock)
            {
                if (_formatContext != null && _headerWritten)
                {
                    try
                    {
                        // 写入尾部信息
                        _formatContext.WriteTrailer();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error writing trailer");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
        finally
        {
            Cleanup();
            SetState(StreamerState.Disconnected);
        }

        return Task.CompletedTask;
    }

    public Task SendPacketAsync(EncodedPacket packet)
    {
        if (!IsConnected || _state == StreamerState.Error)
        {
            // 连接已断开或出错，静默返回
            return Task.CompletedTask;
        }

        if (_formatContext == null || !_videoStreamSet || _options == null)
        {
            throw new InvalidOperationException("Streamer is not properly initialized");
        }

        if (_state == StreamerState.Connected)
        {
            SetState(StreamerState.Streaming);
        }

        try
        {
            lock (_lock)
            {
                // 再次检查状态（锁内检查）
                if (_state == StreamerState.Error || _state == StreamerState.Disconnected)
                {
                    return Task.CompletedTask;
                }

                // 如果头部还没写入，先写入头部
                if (!_headerWritten)
                {
                    // 尝试从第一个关键帧获取 extradata
                    if (packet.IsKeyFrame && packet.Data.Length > 0)
                    {
                        // 对于 H.264，需要检测并提取 SPS/PPS
                        // 这里简化处理，假设编码器已经设置了正确的 extradata
                        WriteHeader();
                    }
                    else
                    {
                        // 等待关键帧
                        _logger.LogDebug("Waiting for keyframe before writing header");
                        return Task.CompletedTask;
                    }
                }

                // 确保 PTS 和 DTS 有效（在创建 packet 之前计算好）
                long pts = packet.Pts;
                long dts = packet.Dts;
                
                // 检查是否是无效值 (AV_NOPTS_VALUE = long.MinValue)
                if (dts == long.MinValue || dts == ffmpeg.AV_NOPTS_VALUE)
                {
                    dts = pts;
                }
                if (pts == long.MinValue || pts == ffmpeg.AV_NOPTS_VALUE)
                {
                    // 如果 PTS 也无效，使用帧计数器作为时间戳
                    pts = _framesSent;
                    dts = _framesSent;
                }

                // 手动计算时间戳转换 (从编码器时间基准转换到流时间基准)
                // srcTimeBase = 1/fps, dstTimeBase = 1/1000 (毫秒)
                // pts_dst = pts_src * (srcTimeBase.num * dstTimeBase.den) / (srcTimeBase.den * dstTimeBase.num)
                var srcTimeBase = new AVRational(packet.TimeBaseNum, packet.TimeBaseDen);
                var dstTimeBase = _videoStream.TimeBase;
                
                // 转换: pts * (src.num / src.den) * (dst.den / dst.num)
                // 简化: pts * src.num * dst.den / (src.den * dst.num)
                long ptsConverted = pts * srcTimeBase.Num * dstTimeBase.Den / (srcTimeBase.Den * dstTimeBase.Num);
                long dtsConverted = dts * srcTimeBase.Num * dstTimeBase.Den / (srcTimeBase.Den * dstTimeBase.Num);

                // 使用原始 FFmpeg API 避免托管/非托管内存问题
                unsafe
                {
                    // 分配 packet
                    AVPacket* pkt = ffmpeg.av_packet_alloc();
                    if (pkt == null)
                    {
                        throw new OutOfMemoryException("Failed to allocate AVPacket");
                    }

                    try
                    {
                        var bufferSize = packet.Data.Length;
                        
                        // 分配数据 buffer
                        var buffer = (byte*)ffmpeg.av_malloc((ulong)bufferSize);
                        if (buffer == null)
                        {
                            throw new OutOfMemoryException("Failed to allocate FFmpeg buffer");
                        }
                        
                        // 复制数据到 FFmpeg 分配的内存
                        fixed (byte* srcPtr = packet.Data)
                        {
                            Buffer.MemoryCopy(srcPtr, buffer, bufferSize, bufferSize);
                        }
                        
                        // 设置到 packet
                        var ret = ffmpeg.av_packet_from_data(pkt, buffer, bufferSize);
                        if (ret < 0)
                        {
                            ffmpeg.av_free(buffer);
                            throw new InvalidOperationException($"av_packet_from_data failed: {ret}");
                        }

                        // 设置时间戳和标志
                        pkt->pts = ptsConverted;
                        pkt->dts = dtsConverted;
                        pkt->stream_index = _videoStream.Index;
                        if (packet.IsKeyFrame)
                        {
                            pkt->flags |= (int)ffmpeg.AV_PKT_FLAG_KEY;
                        }

                        // 写入数据包
                        ret = ffmpeg.av_interleaved_write_frame(_formatContext, pkt);
                        if (ret < 0)
                        {
                            throw new InvalidOperationException($"av_interleaved_write_frame failed: {ret}");
                        }

                        _bytesSent += packet.Data.Length;
                        _framesSent++;

                        _logger.LogDebug("Sent packet: size={Size}, pts={Pts}, dts={Dts}, keyframe={IsKeyFrame}",
                            packet.Data.Length, ptsConverted, dtsConverted, packet.IsKeyFrame);
                    }
                    finally
                    {
                        ffmpeg.av_packet_free(&pkt);
                    }
                }
            }

            // 定期更新统计信息
            if (_framesSent % 30 == 0)
            {
                UpdateStats();
            }

            return Task.CompletedTask;
        }
        catch (FFmpegException ex) when (ex.Message.Contains("-10053") || ex.Message.Contains("-10054"))
        {
            // 网络连接错误，标记为断开
            _logger.LogWarning("RTMP connection lost: {Message}", ex.Message);
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
    public unsafe void SetExtraData(byte[] extraData)
    {
        if (_videoStreamSet && _videoStream.Codecpar != null && extraData.Length > 0)
        {
            var codecpar = _videoStream.Codecpar;
            // 分配并复制 extradata
            var ptr = ffmpeg.av_malloc((ulong)extraData.Length);
            System.Runtime.InteropServices.Marshal.Copy(extraData, 0, (IntPtr)ptr, extraData.Length);
            codecpar.Extradata = (IntPtr)ptr;
            codecpar.ExtradataSize = extraData.Length;
            _logger.LogInformation("Set extradata: {Length} bytes", extraData.Length);
        }
    }

    private void WriteHeader()
    {
        if (_formatContext == null || _headerWritten) return;

        try
        {
            _formatContext.WriteHeader();
            _headerWritten = true;
            _logger.LogInformation("RTMP header written");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write RTMP header");
            throw;
        }
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
            _formatContext?.Pb?.Dispose();
        }
        catch { }

        try
        {
            _formatContext?.Dispose();
        }
        catch { }

        _formatContext = null;
        _videoStreamSet = false;
        _headerWritten = false;
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
