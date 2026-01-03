using Microsoft.JSInterop;

namespace YASS.Web.Services;

/// <summary>
/// 播放器服务接口 - 便于后续切换前端框架
/// </summary>
public interface IPlayerService
{
    Task InitializeAsync(string elementId, string url);
    Task PlayAsync();
    Task PauseAsync();
    Task StopAsync();
    Task DisposeAsync();
    bool IsPlaying { get; }
}

/// <summary>
/// FLV.js 播放器服务实现
/// </summary>
public class FlvPlayerService : IPlayerService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private bool _isInitialized;
    private string? _elementId;

    public bool IsPlaying { get; private set; }

    public FlvPlayerService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync(string elementId, string url)
    {
        _elementId = elementId;
        await _jsRuntime.InvokeVoidAsync("flvPlayer.initialize", elementId, url);
        _isInitialized = true;
    }

    public async Task PlayAsync()
    {
        if (!_isInitialized) return;
        await _jsRuntime.InvokeVoidAsync("flvPlayer.play");
        IsPlaying = true;
    }

    public async Task PauseAsync()
    {
        if (!_isInitialized) return;
        await _jsRuntime.InvokeVoidAsync("flvPlayer.pause");
        IsPlaying = false;
    }

    public async Task StopAsync()
    {
        if (!_isInitialized) return;
        await _jsRuntime.InvokeVoidAsync("flvPlayer.stop");
        IsPlaying = false;
    }

    public async Task DisposeAsync()
    {
        if (_isInitialized)
        {
            await _jsRuntime.InvokeVoidAsync("flvPlayer.dispose");
            _isInitialized = false;
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
