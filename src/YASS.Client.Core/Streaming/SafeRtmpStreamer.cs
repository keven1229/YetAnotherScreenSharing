using Microsoft.Extensions.Logging;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Formats;
using Sdcb.FFmpeg.Raw;
using System.Threading.Channels;
using YASS.Client.Core.Interfaces;
using YASS.Shared.Enums;

namespace YASS.Client.Core.Streaming;

/// <summary>
/// 线程安全的 RTMP 推流器 - 使用专用线程处理 FFmpeg 操作
/// 通过 Channel 队列确保所有 FFmpeg 操作在同一线程执行
/// </summary>
public class SafeRtmpStreamer : IStreamer
{
    private readonly ILogger<SafeRtmpStreamer> _logger;

    private StreamerState _state = StreamerState.Disconnected;
    private StreamerOptions? _options;

    // 统计信息
    private long _bytesSent;
    private long _framesSent;
    private long _framesDropped;
    private DateTime _startTime;

    // FFmpeg 资源 - 仅在专用线程中访问
    private FormatContext? _formatContext;
    private MediaStream _videoStream;
    private bool _headerWritten;
    private bool _videoStreamSet;
    private byte[]? _extraData;

    // 专用写入线程
    private Channel<WriteRequest>? _writeChannel;
    private Task? _writerTask;
    private CancellationTokenSource? _cts;

    public bool IsConnected => _state == StreamerState.Connected || _state == StreamerState.Streaming;

    public event EventHandler<StreamerStateChangedEventArgs>? StateChanged;
    public event EventHandler<StreamerStatsEventArgs>? StatsUpdated;

    public SafeRtmpStreamer(ILogger<SafeRtmpStreamer> logger)
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
        _cts = new CancellationTokenSource();
        SetState(StreamerState.Connecting);

        try
        {
            _logger.LogInformation("Connecting to RTMP server: {Url}", options.Url);
            _logger.LogInformation("Stream config: {Width}x{Height}@{Fps}fps, codec: {Codec}",
                options.Width, options.Height, options.FrameRate, options.CodecType);

            // 创建输出格式上下文 (FLV 格式用于 RTMP)
            _formatContext = FormatContext.AllocOutput(formatName: "flv");

            // 查找对应的编码器
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
            _videoStream.TimeBase = new AVRational(1, 1000);

            // 打开输出 URL
            _formatContext.Pb = IOContext.OpenWrite(options.Url);
            _headerWritten = false;

            // 创建写入队列和专用线程
            _writeChannel = Channel.CreateBounded<WriteRequest>(new BoundedChannelOptions(120)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

            _writerTask = Task.Factory.StartNew(
                () => WriterLoop(_cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

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

    public async Task DisconnectAsync()
    {
        if (_state == StreamerState.Disconnected) return;

        _logger.LogInformation("Disconnecting from RTMP server");

        try
        {
            // 停止写入线程
            _cts?.Cancel();
            _writeChannel?.Writer.TryComplete();

            if (_writerTask != null)
            {
                try
                {
                    await _writerTask.WaitAsync(TimeSpan.FromSeconds(3));
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Writer task did not complete in time");
                }
                catch (OperationCanceledException) { }
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
    }

    public Task SendPacketAsync(EncodedPacket packet)
    {
        if (!IsConnected || _state == StreamerState.Error)
        {
            return Task.CompletedTask;
        }

        if (_writeChannel == null)
        {
            return Task.CompletedTask;
        }

        if (_state == StreamerState.Connected)
        {
            SetState(StreamerState.Streaming);
        }

        // 复制数据以确保线程安全
        var request = new WriteRequest
        {
            Data = packet.Data.ToArray(),
            Pts = packet.Pts,
            Dts = packet.Dts,
            IsKeyFrame = packet.IsKeyFrame,
            TimeBaseNum = packet.TimeBaseNum,
            TimeBaseDen = packet.TimeBaseDen
        };

        // 非阻塞写入队列
        if (!_writeChannel.Writer.TryWrite(request))
        {
            _framesDropped++;
            _logger.LogDebug("Dropped frame due to full queue");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 设置编码器的 extradata（SPS/PPS 等）
    /// </summary>
    public unsafe void SetExtraData(byte[] extraData)
    {
        if (_videoStreamSet && _videoStream.Codecpar != null && extraData.Length > 0)
        {
            _extraData = extraData.ToArray();
            var codecpar = _videoStream.Codecpar;
            var ptr = ffmpeg.av_malloc((ulong)extraData.Length);
            System.Runtime.InteropServices.Marshal.Copy(extraData, 0, (IntPtr)ptr, extraData.Length);
            codecpar.Extradata = (IntPtr)ptr;
            codecpar.ExtradataSize = extraData.Length;
            _logger.LogInformation("Set extradata: {Length} bytes", extraData.Length);
        }
    }

    /// <summary>
    /// 专用写入线程 - 所有 FFmpeg 操作都在这里执行
    /// </summary>
    private void WriterLoop(CancellationToken ct)
    {
        Thread.CurrentThread.Name = "RTMP Writer";
        _logger.LogInformation("Writer thread started");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                WriteRequest request;
                try
                {
                    // 同步等待下一个写入请求
                    if (!_writeChannel!.Reader.TryRead(out request))
                    {
                        // 没有数据，等待一下
                        if (_writeChannel.Reader.WaitToReadAsync(ct).AsTask().GetAwaiter().GetResult())
                        {
                            continue;
                        }
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    WritePacketInternal(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing packet");
                    _framesDropped++;
                    
                    // 检查是否是致命错误
                    if (ex.Message.Contains("-10053") || ex.Message.Contains("-10054"))
                    {
                        SetState(StreamerState.Error, "Connection lost");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Writer thread crashed");
            SetState(StreamerState.Error, ex.Message);
        }
        finally
        {
            // 写入 trailer
            try
            {
                if (_formatContext != null && _headerWritten)
                {
                    _formatContext.WriteTrailer();
                }
            }
            catch { }

            _logger.LogInformation("Writer thread stopped");
        }
    }

    private void WritePacketInternal(WriteRequest request)
    {
        if (_formatContext == null || !_videoStreamSet || _options == null)
            return;

        // 写入头部
        if (!_headerWritten)
        {
            if (request.IsKeyFrame && request.Data.Length > 0)
            {
                _formatContext.WriteHeader();
                _headerWritten = true;
                _logger.LogInformation("RTMP header written");
            }
            else
            {
                return; // 等待关键帧
            }
        }

        // 计算时间戳
        long pts = request.Pts;
        long dts = request.Dts;

        if (dts == long.MinValue || dts == ffmpeg.AV_NOPTS_VALUE)
            dts = pts;
        if (pts == long.MinValue || pts == ffmpeg.AV_NOPTS_VALUE)
        {
            pts = _framesSent;
            dts = _framesSent;
        }

        // 时间戳转换
        var srcTimeBase = new AVRational(request.TimeBaseNum, request.TimeBaseDen);
        var dstTimeBase = _videoStream.TimeBase;
        long ptsConverted = pts * srcTimeBase.Num * dstTimeBase.Den / (srcTimeBase.Den * dstTimeBase.Num);
        long dtsConverted = dts * srcTimeBase.Num * dstTimeBase.Den / (srcTimeBase.Den * dstTimeBase.Num);

        // 使用原始 FFmpeg API
        unsafe
        {
            AVPacket* pkt = ffmpeg.av_packet_alloc();
            if (pkt == null)
            {
                throw new OutOfMemoryException("Failed to allocate AVPacket");
            }

            try
            {
                var bufferSize = request.Data.Length;
                
                // 分配数据 buffer (由 FFmpeg 管理)
                var buffer = (byte*)ffmpeg.av_malloc((ulong)bufferSize);
                if (buffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate FFmpeg buffer");
                }
                
                // 复制数据到 FFmpeg 分配的内存
                fixed (byte* srcPtr = request.Data)
                {
                    Buffer.MemoryCopy(srcPtr, buffer, bufferSize, bufferSize);
                }
                
                // 使用 av_packet_from_data 关联 buffer 到 packet
                // 这样 FFmpeg 会在 packet 释放时自动释放 buffer
                int ret = ffmpeg.av_packet_from_data(pkt, buffer, bufferSize);
                if (ret < 0)
                {
                    ffmpeg.av_free(buffer);
                    throw new InvalidOperationException($"av_packet_from_data failed: {ret}");
                }

                // 设置 packet 属性
                pkt->pts = ptsConverted;
                pkt->dts = dtsConverted;
                pkt->stream_index = _videoStream.Index;

                if (request.IsKeyFrame)
                {
                    pkt->flags |= (int)ffmpeg.AV_PKT_FLAG_KEY;
                }

                // 写入
                ret = ffmpeg.av_interleaved_write_frame(_formatContext, pkt);
                if (ret < 0)
                {
                    throw new InvalidOperationException($"av_interleaved_write_frame failed: {ret}");
                }

                _bytesSent += bufferSize;
                _framesSent++;

                _logger.LogDebug("Sent packet: size={Size}, pts={Pts}, dts={Dts}, keyframe={IsKeyFrame}",
                    bufferSize, ptsConverted, dtsConverted, request.IsKeyFrame);
            }
            finally
            {
                ffmpeg.av_packet_free(&pkt);
            }
        }

        // 更新统计
        if (_framesSent % 30 == 0)
        {
            UpdateStats();
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
        try { _cts?.Cancel(); } catch { }
        try { _writeChannel?.Writer.TryComplete(); } catch { }
        try { _formatContext?.Pb?.Dispose(); } catch { }
        try { _formatContext?.Dispose(); } catch { }

        _formatContext = null;
        _videoStreamSet = false;
        _headerWritten = false;
        _writeChannel = null;
        _writerTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    private struct WriteRequest
    {
        public byte[] Data;
        public long Pts;
        public long Dts;
        public bool IsKeyFrame;
        public int TimeBaseNum;
        public int TimeBaseDen;
    }
}
