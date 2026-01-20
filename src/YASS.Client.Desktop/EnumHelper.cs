using YASS.Shared.Enums;

namespace YASS.Client.Desktop;

/// <summary>
/// 枚举帮助类，用于 XAML 绑定
/// </summary>
public static class EnumHelper
{
    public static IReadOnlyList<CodecType> CodecTypes { get; } = Enum.GetValues<CodecType>();
    public static IReadOnlyList<EncoderType> EncoderTypes { get; } = Enum.GetValues<EncoderType>();
}
