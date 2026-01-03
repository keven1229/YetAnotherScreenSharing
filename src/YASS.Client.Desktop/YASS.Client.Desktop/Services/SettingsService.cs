using System.IO;
using System.Text.Json;
using YASS.Shared.Enums;
using YASS.Shared.Models;

namespace YASS.Client.Desktop.Services;

/// <summary>
/// 设置服务接口
/// </summary>
public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
}

/// <summary>
/// 应用设置
/// </summary>
public class AppSettings
{
    public string ServerUrl { get; set; } = "http://localhost:5000";
    public string RtmpServerUrl { get; set; } = "rtmp://localhost:1935/live";
    public StreamConfig DefaultStreamConfig { get; set; } = new();
    public string? LastRoomId { get; set; }
    public bool CaptureCursor { get; set; } = true;
    public string Theme { get; set; } = "Dark";
}

/// <summary>
/// 设置服务实现
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var yassFolder = Path.Combine(appData, "YASS");
        Directory.CreateDirectory(yassFolder);
        _settingsPath = Path.Combine(yassFolder, "settings.json");
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsPath, json);
    }
}
