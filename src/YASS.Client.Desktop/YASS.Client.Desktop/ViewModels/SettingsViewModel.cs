using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YASS.Client.Desktop.Services;
using YASS.Shared.Enums;
using YASS.Shared.Models;

namespace YASS.Client.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private string _rtmpServerUrl = string.Empty;

    [ObservableProperty]
    private int _width = 1920;

    [ObservableProperty]
    private int _height = 1080;

    [ObservableProperty]
    private int _frameRate = 30;

    [ObservableProperty]
    private int _videoBitrate = 4000;

    [ObservableProperty]
    private CodecType _selectedCodec = CodecType.H265;

    [ObservableProperty]
    private EncoderType _selectedEncoder = EncoderType.Auto;

    [ObservableProperty]
    private string _selectedPreset = "fast";

    [ObservableProperty]
    private bool _captureCursor = true;

    [ObservableProperty]
    private string _selectedTheme = "Dark";

    public IReadOnlyList<CodecType> AvailableCodecs { get; } = Enum.GetValues<CodecType>();
    public IReadOnlyList<EncoderType> AvailableEncoders { get; } = Enum.GetValues<EncoderType>();
    public IReadOnlyList<string> AvailablePresets { get; } = ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"];
    public IReadOnlyList<string> AvailableThemes { get; } = ["Light", "Dark"];
    public IReadOnlyList<string> CommonResolutions { get; } = ["1920x1080", "2560x1440", "3840x2160", "1280x720"];
    public IReadOnlyList<int> CommonFrameRates { get; } = [24, 30, 60, 120];
    public IReadOnlyList<int> CommonBitrates { get; } = [1000, 2000, 4000, 6000, 8000, 10000, 15000, 20000];

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;
        ServerUrl = settings.ServerUrl;
        RtmpServerUrl = settings.RtmpServerUrl;
        Width = settings.DefaultStreamConfig.Width;
        Height = settings.DefaultStreamConfig.Height;
        FrameRate = settings.DefaultStreamConfig.FrameRate;
        VideoBitrate = settings.DefaultStreamConfig.VideoBitrate;
        SelectedCodec = settings.DefaultStreamConfig.Codec;
        SelectedEncoder = settings.DefaultStreamConfig.Encoder;
        SelectedPreset = settings.DefaultStreamConfig.Preset;
        CaptureCursor = settings.CaptureCursor;
        SelectedTheme = settings.Theme;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = _settingsService.Settings;
        settings.ServerUrl = ServerUrl;
        settings.RtmpServerUrl = RtmpServerUrl;
        settings.DefaultStreamConfig = new StreamConfig
        {
            Width = Width,
            Height = Height,
            FrameRate = FrameRate,
            VideoBitrate = VideoBitrate,
            Codec = SelectedCodec,
            Encoder = SelectedEncoder,
            Preset = SelectedPreset
        };
        settings.CaptureCursor = CaptureCursor;
        settings.Theme = SelectedTheme;

        await _settingsService.SaveAsync();
    }

    [RelayCommand]
    private void SetResolution(string resolution)
    {
        var parts = resolution.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
        {
            Width = w;
            Height = h;
        }
    }
}
