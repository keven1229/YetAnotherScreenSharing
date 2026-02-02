using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YASS.Client.Desktop.Services;
using YASS.Client.Desktop.Views;
using YASS.Shared.Models;

namespace YASS.Client.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private string _title = "YASS - Yet Another Screen Sharing";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private ObservableCollection<RoomInfo> _rooms = [];

    [ObservableProperty]
    private RoomInfo? _selectedRoom;

    [ObservableProperty]
    private string _newRoomName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrivacyModeTooltip))]
    private bool _isPrivacyMode;

    /// <summary>
    /// 隐私模式提示文本
    /// </summary>
    public string PrivacyModeTooltip => "启用后房间列表不显示预览图";

    public MainViewModel(IApiService apiService)
    {
        _apiService = apiService;

        // 加载房间列表
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "正在初始化...";

        try
        {
            // 加载房间列表
            await RefreshRoomsAsync();
            StatusMessage = "就绪";
        }
        catch (Exception ex)
        {
            StatusMessage = $"初始化失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshRoomsAsync()
    {
        try
        {
            IsLoading = true;
            var response = await _apiService.GetRoomsAsync();
            if (response != null)
            {
                Rooms = new ObservableCollection<RoomInfo>(response.Rooms);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"获取房间列表失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateRoomAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRoomName))
        {
            StatusMessage = "请输入房间名称";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "正在创建房间...";
            
            var response = await _apiService.CreateRoomAsync(new Shared.DTOs.CreateRoomRequest
            {
                Name = NewRoomName,
                IsPrivacyMode = IsPrivacyMode
            });

            if (response != null)
            {
                Rooms.Insert(0, response.Room);
                SelectedRoom = response.Room;
                NewRoomName = string.Empty;
                StatusMessage = $"房间 '{response.Room.Name}' 创建成功";
            }
            else
            {
                StatusMessage = "创建房间失败: 服务器返回空响应";
            }
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"网络错误: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"创建房间失败: {ex.GetType().Name} - {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenStreamingView()
    {
        if (SelectedRoom == null)
        {
            StatusMessage = "请选择或创建房间";
            return;
        }

        // 创建推流视图
        var streamingViewModel = App.Services.GetRequiredService<StreamingViewModel>();
        streamingViewModel.Initialize(SelectedRoom);

        // 打开推流窗口
        var streamingPage = new StreamingPage(streamingViewModel);
        streamingPage.Owner = System.Windows.Application.Current.MainWindow;
        streamingPage.Show();
    }

    [RelayCommand]
    private async Task DeleteRoomAsync(RoomInfo? room)
    {
        if (room == null)
        {
            StatusMessage = "请选择要删除的房间";
            return;
        }

        // 确认删除
        var result = System.Windows.MessageBox.Show(
            $"确定要删除房间 '{room.Name}' 吗？",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "正在删除房间...";

            var success = await _apiService.DeleteRoomAsync(room.Id);
            if (success)
            {
                Rooms.Remove(room);
                if (SelectedRoom == room)
                {
                    SelectedRoom = null;
                }
                StatusMessage = $"房间 '{room.Name}' 已删除";
            }
            else
            {
                StatusMessage = "删除房间失败";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除房间失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
