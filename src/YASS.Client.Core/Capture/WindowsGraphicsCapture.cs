using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using YASS.Client.Core.Interfaces;

namespace YASS.Client.Core.Capture;

/// <summary>
/// Windows Graphics Capture API 实现
/// </summary>
public class WindowsGraphicsCapture : IScreenCapture
{
    private readonly ILogger<WindowsGraphicsCapture> _logger;
    private bool _isCapturing;
    private CancellationTokenSource? _captureTokenSource;
    private Task? _captureTask;

    public bool IsCapturing => _isCapturing;

    public event EventHandler<FrameArrivedEventArgs>? FrameArrived;
    public event EventHandler<CaptureStoppedEventArgs>? CaptureStopped;

    public WindowsGraphicsCapture(ILogger<WindowsGraphicsCapture> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<CaptureSource>> GetCaptureSourcesAsync()
    {
        var sources = new List<CaptureSource>();

        // 枚举显示器
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
        {
            var monitorInfo = new MONITORINFOEX();
            monitorInfo.cbSize = Marshal.SizeOf<MONITORINFOEX>();
            
            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var width = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
                var height = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;
                var isPrimary = (monitorInfo.dwFlags & MONITORINFOF_PRIMARY) != 0;

                sources.Add(new CaptureSource
                {
                    Handle = hMonitor,
                    Type = CaptureSourceType.Monitor,
                    DisplayName = isPrimary ? $"主显示器 ({width}x{height})" : $"显示器 {sources.Count + 1} ({width}x{height})",
                    Width = width,
                    Height = height,
                    IsPrimary = isPrimary
                });
            }
            return true;
        }, IntPtr.Zero);

        // 枚举窗口
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var titleLength = GetWindowTextLength(hWnd);
            if (titleLength == 0) return true;

            var title = new char[titleLength + 1];
            GetWindowText(hWnd, title, title.Length);
            var windowTitle = new string(title).TrimEnd('\0');

            if (string.IsNullOrWhiteSpace(windowTitle)) return true;

            // 排除一些系统窗口
            if (windowTitle == "Program Manager" || 
                windowTitle == "Windows Shell Experience Host" ||
                windowTitle == "Microsoft Text Input Application")
                return true;

            GetWindowRect(hWnd, out var rect);
            var width = rect.right - rect.left;
            var height = rect.bottom - rect.top;

            if (width > 100 && height > 100) // 过滤太小的窗口
            {
                sources.Add(new CaptureSource
                {
                    Handle = hWnd,
                    Type = CaptureSourceType.Window,
                    DisplayName = windowTitle,
                    Width = width,
                    Height = height,
                    IsPrimary = false
                });
            }

            return true;
        }, IntPtr.Zero);

        return Task.FromResult<IReadOnlyList<CaptureSource>>(sources);
    }

    public async Task StartCaptureAsync(CaptureSource source, CaptureOptions options)
    {
        if (_isCapturing)
        {
            throw new InvalidOperationException("Capture is already running");
        }

        _logger.LogInformation("Starting capture for {SourceType}: {SourceName}", source.Type, source.DisplayName);

        _isCapturing = true;
        _captureTokenSource = new CancellationTokenSource();

        // 使用 GDI BitBlt 实现屏幕捕获
        _captureTask = Task.Run(async () =>
        {
            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / options.TargetFrameRate);
            var timestamp = 0L;
            var width = options.OutputWidth > 0 ? options.OutputWidth : source.Width;
            var height = options.OutputHeight > 0 ? options.OutputHeight : source.Height;

            // 获取屏幕 DC
            IntPtr hdcScreen = IntPtr.Zero;
            IntPtr hdcMemory = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBitmap = IntPtr.Zero;

            try
            {
                if (source.Type == CaptureSourceType.Monitor)
                {
                    // 获取显示器信息
                    var monitorInfo = new MONITORINFOEX();
                    monitorInfo.cbSize = Marshal.SizeOf<MONITORINFOEX>();
                    GetMonitorInfo(source.Handle, ref monitorInfo);
                    
                    hdcScreen = CreateDC(monitorInfo.szDevice, null!, IntPtr.Zero, IntPtr.Zero);
                }
                else
                {
                    hdcScreen = GetDC(IntPtr.Zero); // 整个屏幕
                }

                if (hdcScreen == IntPtr.Zero)
                {
                    throw new Exception("Failed to get screen DC");
                }

                hdcMemory = CreateCompatibleDC(hdcScreen);
                hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
                hOldBitmap = SelectObject(hdcMemory, hBitmap);

                // 准备 BITMAPINFO
                var bmi = new BITMAPINFO
                {
                    bmiHeader = new BITMAPINFOHEADER
                    {
                        biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                        biWidth = width,
                        biHeight = -height, // 负值表示自顶向下
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0, // BI_RGB
                        biSizeImage = (uint)(width * height * 4)
                    }
                };

                var bufferSize = width * height * 4;
                var bufferHandle = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    while (!_captureTokenSource.Token.IsCancellationRequested)
                    {
                        var frameStart = DateTime.UtcNow;
                        
                        try
                        {
                            // 捕获屏幕
                            if (source.Type == CaptureSourceType.Window)
                            {
                                // 窗口捕获
                                GetWindowRect(source.Handle, out var rect);
                                var windowDc = GetWindowDC(source.Handle);
                                StretchBlt(hdcMemory, 0, 0, width, height, windowDc, 0, 0, 
                                    rect.right - rect.left, rect.bottom - rect.top, SRCCOPY);
                                ReleaseDC(source.Handle, windowDc);
                            }
                            else
                            {
                                // 显示器捕获
                                BitBlt(hdcMemory, 0, 0, width, height, hdcScreen, 0, 0, SRCCOPY);
                            }

                            // 获取像素数据
                            GetDIBits(hdcMemory, hBitmap, 0, (uint)height, bufferHandle, ref bmi, 0);

                            // 触发帧事件
                            timestamp += (long)frameInterval.TotalMilliseconds;
                            OnFrameArrived(new FrameArrivedEventArgs
                            {
                                Data = bufferHandle,
                                Size = bufferSize,
                                Width = width,
                                Height = height,
                                Stride = width * 4,
                                Timestamp = timestamp,
                                Format = PixelFormat.BGRA32
                            });

                            // 等待下一帧
                            var elapsed = DateTime.UtcNow - frameStart;
                            var delay = frameInterval - elapsed;
                            if (delay > TimeSpan.Zero)
                            {
                                await Task.Delay(delay, _captureTokenSource.Token);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during capture");
                            OnCaptureStopped(CaptureStopReason.Error, ex.Message);
                            break;
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(bufferHandle);
                }
            }
            finally
            {
                // 清理 GDI 资源
                if (hOldBitmap != IntPtr.Zero && hdcMemory != IntPtr.Zero)
                    SelectObject(hdcMemory, hOldBitmap);
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
                if (hdcMemory != IntPtr.Zero)
                    DeleteDC(hdcMemory);
                if (hdcScreen != IntPtr.Zero)
                {
                    if (source.Type == CaptureSourceType.Monitor)
                        DeleteDC(hdcScreen);
                    else
                        ReleaseDC(IntPtr.Zero, hdcScreen);
                }
            }
        }, _captureTokenSource.Token);

        await Task.CompletedTask;
    }

    public async Task StopCaptureAsync()
    {
        if (!_isCapturing) return;

        _logger.LogInformation("Stopping capture");

        _captureTokenSource?.Cancel();

        if (_captureTask != null)
        {
            try
            {
                await _captureTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _isCapturing = false;
        OnCaptureStopped(CaptureStopReason.UserRequested, null);
    }

    protected virtual void OnFrameArrived(FrameArrivedEventArgs e)
    {
        FrameArrived?.Invoke(this, e);
    }

    protected virtual void OnCaptureStopped(CaptureStopReason reason, string? error)
    {
        CaptureStopped?.Invoke(this, new CaptureStoppedEventArgs
        {
            Reason = reason,
            Error = error
        });
    }

    public void Dispose()
    {
        _captureTokenSource?.Cancel();
        _captureTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region P/Invoke

    private const int MONITORINFOF_PRIMARY = 0x00000001;
    private const int SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public uint[] bmiColors;
    }

    private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDC(string lpszDriver, string? lpszDevice, IntPtr lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, IntPtr lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    #endregion
}
