using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Swscales;
using Sdcb.FFmpeg.Utils;
using YASS.Client.Core.Interfaces;

namespace YASS.Client.Core.Encoding;

/// <summary>
/// 视频帧格式转换器
/// </summary>
public class VideoFrameFormatConverter : IDisposable
{
    private Sdcb.FFmpeg.Swscales.VideoFrameConverter? _converter;
    private Frame? _destFrame;
    private int _srcWidth;
    private int _srcHeight;
    private AVPixelFormat _srcFormat;
    private int _dstWidth;
    private int _dstHeight;
    private AVPixelFormat _dstFormat;
    private bool _initialized;

    /// <summary>
    /// 初始化转换器
    /// </summary>
    public void Initialize(int srcWidth, int srcHeight, AVPixelFormat srcFormat,
                          int dstWidth, int dstHeight, AVPixelFormat dstFormat)
    {
        if (_initialized &&
            _srcWidth == srcWidth && _srcHeight == srcHeight && _srcFormat == srcFormat &&
            _dstWidth == dstWidth && _dstHeight == dstHeight && _dstFormat == dstFormat)
        {
            // 参数相同，无需重新初始化
            return;
        }

        Dispose();

        _srcWidth = srcWidth;
        _srcHeight = srcHeight;
        _srcFormat = srcFormat;
        _dstWidth = dstWidth;
        _dstHeight = dstHeight;
        _dstFormat = dstFormat;

        _converter = new Sdcb.FFmpeg.Swscales.VideoFrameConverter();
        _destFrame = Frame.CreateVideo(dstWidth, dstHeight, dstFormat);
        _initialized = true;
    }

    /// <summary>
    /// 转换帧格式
    /// </summary>
    public unsafe Frame Convert(nint srcData, int srcWidth, int srcHeight, int srcStride, AVPixelFormat srcFormat)
    {
        if (!_initialized || _converter == null || _destFrame == null)
        {
            throw new InvalidOperationException("Converter not initialized");
        }

        using var srcFrame = new Frame
        {
            Width = srcWidth,
            Height = srcHeight,
            Format = (int)srcFormat
        };
        srcFrame.Data[0] = (IntPtr)srcData;
        srcFrame.Linesize[0] = srcStride;

        _converter.ConvertFrame(srcFrame, _destFrame);
        return _destFrame;
    }

    /// <summary>
    /// 转换帧格式 (从 Frame 到 Frame)
    /// </summary>
    public Frame Convert(Frame srcFrame)
    {
        if (!_initialized || _converter == null || _destFrame == null)
        {
            throw new InvalidOperationException("Converter not initialized");
        }

        _converter.ConvertFrame(srcFrame, _destFrame);
        return _destFrame;
    }

    public void Dispose()
    {
        _destFrame?.Dispose();
        _destFrame = null;

        _converter?.Dispose();
        _converter = null;

        _initialized = false;
    }
}

/// <summary>
/// 像素格式转换工具
/// </summary>
public static class PixelFormatHelper
{
    /// <summary>
    /// 将应用层像素格式转换为 FFmpeg 像素格式
    /// </summary>
    public static AVPixelFormat ToFFmpegFormat(PixelFormat format)
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

    /// <summary>
    /// 将 FFmpeg 像素格式转换为应用层像素格式
    /// </summary>
    public static PixelFormat FromFFmpegFormat(AVPixelFormat format)
    {
        return format switch
        {
            AVPixelFormat.Bgra => PixelFormat.BGRA32,
            AVPixelFormat.Rgba => PixelFormat.RGBA32,
            AVPixelFormat.Rgb24 => PixelFormat.RGB24,
            AVPixelFormat.Bgr24 => PixelFormat.BGR24,
            AVPixelFormat.Nv12 => PixelFormat.NV12,
            AVPixelFormat.Yuv420p => PixelFormat.YUV420P,
            _ => PixelFormat.BGRA32
        };
    }

    /// <summary>
    /// 获取像素格式的每像素位数
    /// </summary>
    public static int GetBitsPerPixel(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.BGRA32 => 32,
            PixelFormat.RGBA32 => 32,
            PixelFormat.RGB24 => 24,
            PixelFormat.BGR24 => 24,
            PixelFormat.NV12 => 12,
            PixelFormat.YUV420P => 12,
            _ => 32
        };
    }

    /// <summary>
    /// 计算帧数据大小
    /// </summary>
    public static int CalculateFrameSize(int width, int height, PixelFormat format)
    {
        var bitsPerPixel = GetBitsPerPixel(format);
        return (width * height * bitsPerPixel + 7) / 8;
    }
}
