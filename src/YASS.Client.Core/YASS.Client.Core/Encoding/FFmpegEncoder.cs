using Microsoft.Extensions.Logging;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Swscales;
using Sdcb.FFmpeg.Utils;
using YASS.Client.Core.Interfaces;
using YASS.Shared.Enums;

namespace YASS.Client.Core.Encoding;

/// <summary>
/// FFmpeg 编码器实现 - 使用 Sdcb.FFmpeg 库
/// </summary>
public class FFmpegEncoder : IEncoder
{
    private readonly ILogger<FFmpegEncoder> _logger;
    private readonly object _encodeLock = new();

    private bool _isInitialized;
    private EncoderType _currentEncoderType;
    private CodecType _currentCodecType;
    private EncoderOptions? _options;
    private long _frameIndex;

    // FFmpeg 资源
    private CodecContext? _codecContext;
    private VideoFrameConverter? _frameConverter;
    private Frame? _sourceFrame;
    private Frame? _convertedFrame;
    private AVRational _timeBase;

    public bool IsInitialized => _isInitialized;
    public EncoderType CurrentEncoderType => _currentEncoderType;
    public CodecType CurrentCodecType => _currentCodecType;

    /// <summary>
    /// 获取编码器时间基准（用于时间戳转换）
    /// </summary>
    public AVRational TimeBase => _timeBase;

    public FFmpegEncoder(ILogger<FFmpegEncoder> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(EncoderOptions options)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException("Encoder is already initialized");
        }

        _options = options;
        _currentCodecType = options.CodecType;

        // 确定要使用的编码器类型
        _currentEncoderType = options.EncoderType == EncoderType.Auto
            ? EncoderDetector.GetBestEncoder(options.CodecType)
            : options.EncoderType;

        var encoderName = EncoderDetector.GetFFmpegEncoderName(_currentEncoderType, _currentCodecType);
        _logger.LogInformation("Initializing encoder: {EncoderName} ({EncoderType}, {CodecType})",
            encoderName, _currentEncoderType, _currentCodecType);

        try
        {
            // 查找编码器
            var codec = Codec.FindEncoderByName(encoderName);
            if (codec == null)
            {
                // 尝试回退到软件编码器
                _logger.LogWarning("Encoder {EncoderName} not found, falling back to software encoder", encoderName);
                _currentEncoderType = EncoderType.Software;
                encoderName = EncoderDetector.GetFFmpegEncoderName(_currentEncoderType, _currentCodecType);
                codec = Codec.FindEncoderByName(encoderName);

                if (codec == null)
                {
                    throw new InvalidOperationException($"Cannot find encoder: {encoderName}");
                }
            }

            // 计算时间基准
            _timeBase = new AVRational(1, options.FrameRate);

            // 创建编码器上下文
            _codecContext = new CodecContext(codec)
            {
                Width = options.Width,
                Height = options.Height,
                TimeBase = _timeBase,
                PixelFormat = AVPixelFormat.Yuv420p, // 大多数编码器使用 YUV420P
                BitRate = options.Bitrate * 1000L, // 转换为 bps
                GopSize = options.FrameRate * options.KeyFrameIntervalSeconds, // 关键帧间隔
                MaxBFrames = _currentCodecType == CodecType.H264 ? 2 : 0,
                Flags = AV_CODEC_FLAG.GlobalHeader, // RTMP 需要全局头
            };

            // 设置编码器特定选项
            var codecOptions = CreateCodecOptions(options);

            // 打开编码器
            _codecContext.Open(codec, codecOptions);

            // 创建帧转换器 (BGRA -> YUV420P)
            _frameConverter = new VideoFrameConverter();

            // 创建源帧 (用于接收 BGRA 数据)
            _sourceFrame = new Frame
            {
                Width = options.Width,
                Height = options.Height,
                Format = (int)ConvertPixelFormat(options.InputFormat)
            };

            // 创建转换后的帧 (YUV420P)
            _convertedFrame = Frame.CreateVideo(options.Width, options.Height, AVPixelFormat.Yuv420p);

            _frameIndex = 0;
            _isInitialized = true;

            _logger.LogInformation("Encoder initialized successfully: {Width}x{Height}@{Fps}fps, {Bitrate}kbps, GOP={Gop}",
                options.Width, options.Height, options.FrameRate, options.Bitrate, _codecContext.GopSize);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize encoder");
            Cleanup();
            throw;
        }
    }

    public unsafe Task<EncodedPacket?> EncodeAsync(nint frameData, int width, int height, int stride, PixelFormat format, long timestamp)
    {
        if (!_isInitialized || _options == null || _codecContext == null || 
            _frameConverter == null || _sourceFrame == null || _convertedFrame == null)
        {
            throw new InvalidOperationException("Encoder is not initialized");
        }

        lock (_encodeLock)
        {
            try
            {
                // 设置源帧数据
                _sourceFrame.Data[0] = (IntPtr)frameData;
                _sourceFrame.Linesize[0] = stride;
                _sourceFrame.Width = width;
                _sourceFrame.Height = height;
                _sourceFrame.Format = (int)ConvertPixelFormat(format);

                // 转换帧格式 (BGRA -> YUV420P)
                _frameConverter.ConvertFrame(_sourceFrame, _convertedFrame);

                // 设置时间戳 (将毫秒转换为帧索引)
                _convertedFrame.Pts = _frameIndex;
                _frameIndex++;

                // 编码帧
                _codecContext.SendFrame(_convertedFrame);

                // 尝试接收编码后的数据包
                using var packet = new Packet();
                var result = _codecContext.ReceivePacket(packet);

                if (result == CodecResult.Success)
                {
                    var encodedPacket = new EncodedPacket
                    {
                        Data = packet.Data.ToArray(),
                        Dts = packet.Dts,
                        Pts = packet.Pts,
                        IsKeyFrame = (packet.Flags & ffmpeg.AV_PKT_FLAG_KEY) != 0,
                        TimeBaseNum = _timeBase.Num,
                        TimeBaseDen = _timeBase.Den
                    };

                    _logger.LogDebug("Encoded frame {FrameIndex}: size={Size}, pts={Pts}, keyframe={IsKeyFrame}",
                        _frameIndex, encodedPacket.Data.Length, encodedPacket.Pts, encodedPacket.IsKeyFrame);

                    return Task.FromResult<EncodedPacket?>(encodedPacket);
                }
                else if (result == CodecResult.Again)
                {
                    // 需要更多帧才能产生输出
                    _logger.LogDebug("Encoder needs more frames (frame {FrameIndex})", _frameIndex);
                    return Task.FromResult<EncodedPacket?>(null);
                }
                else
                {
                    _logger.LogWarning("Unexpected encoder result: {Result}", result);
                    return Task.FromResult<EncodedPacket?>(null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encoding frame {FrameIndex}", _frameIndex);
                throw;
            }
        }
    }

    public Task<IReadOnlyList<EncodedPacket>> FlushAsync()
    {
        if (!_isInitialized || _codecContext == null)
        {
            return Task.FromResult<IReadOnlyList<EncodedPacket>>([]);
        }

        _logger.LogInformation("Flushing encoder");

        var packets = new List<EncodedPacket>();

        lock (_encodeLock)
        {
            try
            {
                // 发送空帧以触发编码器刷新
                _codecContext.SendFrame(null);

                // 接收所有剩余的数据包
                while (true)
                {
                    using var packet = new Packet();
                    var result = _codecContext.ReceivePacket(packet);

                    if (result == CodecResult.Success)
                    {
                        packets.Add(new EncodedPacket
                        {
                            Data = packet.Data.ToArray(),
                            Dts = packet.Dts,
                            Pts = packet.Pts,
                            IsKeyFrame = (packet.Flags & ffmpeg.AV_PKT_FLAG_KEY) != 0,
                            TimeBaseNum = _timeBase.Num,
                            TimeBaseDen = _timeBase.Den
                        });
                    }
                    else if (result == CodecResult.EOF || result == CodecResult.Again)
                    {
                        break;
                    }
                }

                _logger.LogInformation("Flushed {Count} packets from encoder", packets.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing encoder");
            }
        }

        return Task.FromResult<IReadOnlyList<EncodedPacket>>(packets);
    }

    /// <summary>
    /// 获取编码器的 extradata (SPS/PPS 等)，用于 RTMP 推流
    /// </summary>
    public unsafe byte[]? GetExtraData()
    {
        if (_codecContext == null) return null;

        var extradataSize = _codecContext.ExtradataSize;
        if (extradataSize <= 0)
        {
            return null;
        }

        var extradataPtr = _codecContext.Extradata;
        if (extradataPtr == IntPtr.Zero)
        {
            return null;
        }

        var extraData = new byte[extradataSize];
        System.Runtime.InteropServices.Marshal.Copy(extradataPtr, extraData, 0, extradataSize);
        return extraData;
    }

    private MediaDictionary CreateCodecOptions(EncoderOptions options)
    {
        var dict = new MediaDictionary();

        // 清理 preset 值 - 处理 WPF ComboBoxItem 绑定问题
        var preset = options.Preset ?? "fast";
        if (preset.Contains(':'))
        {
            // 处理 "System.Windows.Controls.ComboBoxItem: fast" 格式
            preset = preset.Split(':').Last().Trim();
        }
        // 验证 preset 值
        var validPresets = new[] { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow", "placebo" };
        if (!validPresets.Contains(preset, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid preset '{Preset}', falling back to 'fast'", preset);
            preset = "fast";
        }

        switch (_currentEncoderType)
        {
            case EncoderType.NVENC:
                // NVENC 特定选项
                dict["preset"] = preset switch
                {
                    "ultrafast" or "superfast" or "veryfast" => "p1",
                    "faster" or "fast" => "p2",
                    "medium" => "p4",
                    "slow" or "slower" => "p6",
                    "veryslow" => "p7",
                    _ => "p4"
                };
                dict["tune"] = "ll"; // 低延迟
                dict["zerolatency"] = "1";
                dict["rc"] = "cbr"; // 固定码率
                break;

            case EncoderType.AMF:
                // AMD AMF 选项
                dict["quality"] = preset switch
                {
                    "ultrafast" or "superfast" or "veryfast" or "faster" => "speed",
                    "fast" or "medium" => "balanced",
                    _ => "quality"
                };
                dict["rc"] = "cbr";
                break;

            case EncoderType.QSV:
                // Intel QSV 选项
                dict["preset"] = preset switch
                {
                    "ultrafast" or "superfast" or "veryfast" => "veryfast",
                    "faster" or "fast" => "fast",
                    "medium" => "medium",
                    _ => "slow"
                };
                dict["look_ahead"] = "0"; // 禁用前向查看以降低延迟
                break;

            case EncoderType.Software:
            default:
                // libx264/libx265 选项
                dict["preset"] = preset;
                dict["tune"] = "zerolatency"; // 低延迟
                if (_currentCodecType == CodecType.H264)
                {
                    dict["profile"] = "high";
                    dict["level"] = "4.1";
                }
                break;
        }

        return dict;
    }

    private static AVPixelFormat ConvertPixelFormat(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.BGRA32 => AVPixelFormat.Bgra,
            PixelFormat.RGBA32 => AVPixelFormat.Rgba,
            PixelFormat.RGB24 => AVPixelFormat.Rgb24,
            PixelFormat.BGR24 => AVPixelFormat.Bgr24,
            PixelFormat.NV12 => AVPixelFormat.Nv12,
            PixelFormat.YUV420P => AVPixelFormat.Yuv420p,
            _ => AVPixelFormat.Bgra
        };
    }

    private void Cleanup()
    {
        _convertedFrame?.Dispose();
        _convertedFrame = null;

        _sourceFrame?.Dispose();
        _sourceFrame = null;

        _frameConverter?.Dispose();
        _frameConverter = null;

        _codecContext?.Dispose();
        _codecContext = null;

        _isInitialized = false;
    }

    public void Dispose()
    {
        Cleanup();
        _logger.LogInformation("Encoder disposed");
        GC.SuppressFinalize(this);
    }
}
