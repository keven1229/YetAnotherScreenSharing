using Sdcb.FFmpeg.Codecs;
using YASS.Shared.Enums;

namespace YASS.Client.Core.Encoding;

/// <summary>
/// 编码器检测工具
/// </summary>
public static class EncoderDetector
{
    /// <summary>
    /// 检测可用的硬件编码器
    /// </summary>
    public static IReadOnlyList<EncoderCapability> DetectAvailableEncoders()
    {
        var capabilities = new List<EncoderCapability>();

        // 检测 NVIDIA NVENC
        if (IsNvencAvailable())
        {
            capabilities.Add(new EncoderCapability
            {
                EncoderType = EncoderType.NVENC,
                SupportedCodecs = [CodecType.H264, CodecType.H265, CodecType.AV1],
                MaxWidth = 8192,
                MaxHeight = 8192,
                Priority = 1
            });
        }

        // 检测 AMD AMF
        if (IsAmfAvailable())
        {
            capabilities.Add(new EncoderCapability
            {
                EncoderType = EncoderType.AMF,
                SupportedCodecs = [CodecType.H264, CodecType.H265, CodecType.AV1],
                MaxWidth = 4096,
                MaxHeight = 4096,
                Priority = 2
            });
        }

        // 检测 Intel QSV
        if (IsQsvAvailable())
        {
            capabilities.Add(new EncoderCapability
            {
                EncoderType = EncoderType.QSV,
                SupportedCodecs = [CodecType.H264, CodecType.H265, CodecType.AV1],
                MaxWidth = 8192,
                MaxHeight = 8192,
                Priority = 3
            });
        }

        // 软件编码器始终可用
        capabilities.Add(new EncoderCapability
        {
            EncoderType = EncoderType.Software,
            SupportedCodecs = [CodecType.H264, CodecType.H265],
            MaxWidth = 16384,
            MaxHeight = 16384,
            Priority = 100
        });

        return capabilities.OrderBy(c => c.Priority).ToList();
    }

    /// <summary>
    /// 获取最佳编码器
    /// </summary>
    public static EncoderType GetBestEncoder(CodecType codec)
    {
        var capabilities = DetectAvailableEncoders();

        foreach (var cap in capabilities)
        {
            if (cap.SupportedCodecs.Contains(codec))
            {
                return cap.EncoderType;
            }
        }

        return EncoderType.Software;
    }

    /// <summary>
    /// 获取 FFmpeg 编码器名称
    /// </summary>
    public static string GetFFmpegEncoderName(EncoderType encoder, CodecType codec)
    {
        return (encoder, codec) switch
        {
            // NVENC
            (EncoderType.NVENC, CodecType.H264) => "h264_nvenc",
            (EncoderType.NVENC, CodecType.H265) => "hevc_nvenc",
            (EncoderType.NVENC, CodecType.AV1) => "av1_nvenc",

            // AMF
            (EncoderType.AMF, CodecType.H264) => "h264_amf",
            (EncoderType.AMF, CodecType.H265) => "hevc_amf",
            (EncoderType.AMF, CodecType.AV1) => "av1_amf",

            // QSV
            (EncoderType.QSV, CodecType.H264) => "h264_qsv",
            (EncoderType.QSV, CodecType.H265) => "hevc_qsv",
            (EncoderType.QSV, CodecType.AV1) => "av1_qsv",

            // Software
            (EncoderType.Software, CodecType.H264) => "libx264",
            (EncoderType.Software, CodecType.H265) => "libx265",
            (EncoderType.Software, CodecType.AV1) => "libaom-av1",

            // Auto - 不应该到这里
            _ => throw new ArgumentException($"Unsupported encoder/codec combination: {encoder}/{codec}")
        };
    }

    private static bool IsNvencAvailable()
    {
        // 检测 NVIDIA NVENC 支持
        // 方法1: 检查 DLL 是否存在
        try
        {
            var nvidiaPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "nvEncodeAPI64.dll");
            
            if (!File.Exists(nvidiaPath))
            {
                return false;
            }

            // 方法2: 尝试查找 FFmpeg 编码器
            try
            {
                var encoder = Codec.FindEncoderByName("h264_nvenc");
                return encoder != null;
            }
            catch
            {
                return true; // DLL 存在，假设可用
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAmfAvailable()
    {
        // 检测 AMD AMF 支持
        try
        {
            var amfPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "amfrt64.dll");
            
            if (!File.Exists(amfPath))
            {
                return false;
            }

            // 尝试查找 FFmpeg 编码器
            try
            {
                var encoder = Codec.FindEncoderByName("h264_amf");
                return encoder != null;
            }
            catch
            {
                return true; // DLL 存在，假设可用
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsQsvAvailable()
    {
        // 检测 Intel QSV 支持
        try
        {
            // 检查多个可能的 Intel Media SDK DLL
            var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var possibleDlls = new[]
            {
                "mfx_mft_vp9vd_64.dll",
                "mfx_mft_h264ve_64.dll",
                "libmfxhw64.dll"
            };

            var found = possibleDlls.Any(dll => File.Exists(Path.Combine(systemPath, dll)));
            
            if (!found)
            {
                return false;
            }

            // 尝试查找 FFmpeg 编码器
            try
            {
                var encoder = Codec.FindEncoderByName("h264_qsv");
                return encoder != null;
            }
            catch
            {
                return true; // DLL 存在，假设可用
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 验证特定编码器是否真正可用
    /// </summary>
    public static bool IsEncoderAvailable(string encoderName)
    {
        try
        {
            var encoder = Codec.FindEncoderByName(encoderName);
            return encoder != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取所有可用的 FFmpeg 编码器名称
    /// </summary>
    public static IReadOnlyList<string> GetAvailableEncoders(CodecType codecType)
    {
        var encoders = new List<string>();

        var encoderNames = codecType switch
        {
            CodecType.H264 => new[] { "h264_nvenc", "h264_amf", "h264_qsv", "libx264" },
            CodecType.H265 => new[] { "hevc_nvenc", "hevc_amf", "hevc_qsv", "libx265" },
            CodecType.AV1 => new[] { "av1_nvenc", "av1_amf", "av1_qsv", "libaom-av1" },
            _ => Array.Empty<string>()
        };

        foreach (var name in encoderNames)
        {
            if (IsEncoderAvailable(name))
            {
                encoders.Add(name);
            }
        }

        return encoders;
    }
}

/// <summary>
/// 编码器能力信息
/// </summary>
public class EncoderCapability
{
    public EncoderType EncoderType { get; init; }
    public List<CodecType> SupportedCodecs { get; init; } = [];
    public int MaxWidth { get; init; }
    public int MaxHeight { get; init; }
    public int Priority { get; init; }
}
