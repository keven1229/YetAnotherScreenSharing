# 推流使用指南

本文档介绍如何使用外部推流工具（OBS Studio、FFmpeg等）向YASS推送屏幕共享内容。

## 概述

YASS客户端现在提供RTMP推流地址，您可以使用自己熟悉的推流工具将屏幕内容推送到该地址。这种方式更加稳定可靠，并且可以充分利用各种专业推流工具的强大功能。

## 快速开始

### 1. 获取推流地址

1. 启动YASS桌面客户端
2. 选择或创建一个房间
3. 选择捕获源（显示器或窗口）
4. 点击"开始推流"按钮
5. 在弹出的窗口中会显示RTMP推流地址，包括：
   - **服务器地址**：如 `rtmp://localhost:1935/live`
   - **流密钥**：如 `room_abc123_h264`
   - **完整地址**：服务器地址 + "/" + 流密钥

### 2. 复制推流信息

点击各个字段旁边的"复制"按钮，将推流地址复制到剪贴板。

## 使用 OBS Studio 推流

### 安装 OBS Studio

下载地址：[https://obsproject.com/](https://obsproject.com/)

### 配置推流

1. **打开 OBS Studio**

2. **配置推流设置**
   - 点击菜单 `文件` → `设置`
   - 选择左侧的 `推流`
   - 服务：选择 `自定义...`
   - 服务器：粘贴YASS提供的"服务器地址"（例如：`rtmp://localhost:1935/live`）
   - 串流密钥：粘贴YASS提供的"流密钥"（例如：`room_abc123_h264`）
   - 点击 `确定` 保存

3. **添加捕获源**
   - 在 `来源` 面板中点击 `+`
   - 选择 `显示器采集` 或 `窗口采集`
   - 配置捕获设置并确认

4. **推荐的编码设置**
   - 点击菜单 `文件` → `设置` → `输出`
   - 输出模式：`简单` 或 `高级`
   - 编码器：`x264` 或 `NVIDIA NVENC H.264`（如果有NVIDIA显卡）
   - 码率控制：`CBR`（固定码率）
   - 比特率：`2500 - 5000 Kbps`（根据网络情况调整）
   
   在 `视频` 设置中：
   - 输出分辨率：`1920x1080` 或 `1280x720`
   - 常用FPS值：`30`

5. **开始推流**
   - 点击右下角的 `开始推流` 按钮
   - 观察 OBS 底部的状态栏，确认推流正常

### OBS 快捷键

- `F1`：显示/隐藏设置
- `Alt+S`：开始/停止推流
- `Alt+R`：开始/停止录制

## 使用 FFmpeg 推流

### 安装 FFmpeg

下载地址：[https://ffmpeg.org/download.html](https://ffmpeg.org/download.html)

### Windows 桌面捕获推流

```powershell
# 基本命令（使用 gdigrab 捕获桌面）
ffmpeg -f gdigrab -i desktop -c:v libx264 -preset ultrafast -tune zerolatency -f flv rtmp://localhost:1935/live/room_abc123_h264

# 指定帧率和分辨率
ffmpeg -f gdigrab -framerate 30 -video_size 1920x1080 -i desktop -c:v libx264 -preset ultrafast -tune zerolatency -b:v 3000k -f flv rtmp://localhost:1935/live/room_abc123_h264

# 使用硬件加速（NVIDIA NVENC）
ffmpeg -f gdigrab -i desktop -c:v h264_nvenc -preset fast -b:v 3000k -f flv rtmp://localhost:1935/live/room_abc123_h264
```

### macOS 桌面捕获推流

```bash
# 捕获主显示器（屏幕0）
ffmpeg -f avfoundation -i "0" -c:v libx264 -preset ultrafast -tune zerolatency -f flv rtmp://localhost:1935/live/room_abc123_h264

# 列出可用的设备
ffmpeg -f avfoundation -list_devices true -i ""
```

### Linux 桌面捕获推流

```bash
# 使用 x11grab 捕获桌面
ffmpeg -f x11grab -i :0.0 -c:v libx264 -preset ultrafast -tune zerolatency -f flv rtmp://localhost:1935/live/room_abc123_h264

# 指定显示器和偏移
ffmpeg -f x11grab -video_size 1920x1080 -i :0.0+0,0 -c:v libx264 -preset ultrafast -tune zerolatency -b:v 3000k -f flv rtmp://localhost:1935/live/room_abc123_h264
```

### FFmpeg 常用参数说明

| 参数 | 说明 |
|------|------|
| `-f gdigrab` | Windows 桌面捕获 |
| `-f avfoundation` | macOS 设备捕获 |
| `-f x11grab` | Linux X11 桌面捕获 |
| `-i desktop` | 输入源（desktop表示整个桌面）|
| `-framerate 30` | 帧率设置为30fps |
| `-video_size 1920x1080` | 分辨率 |
| `-c:v libx264` | 使用x264编码器 |
| `-c:v h264_nvenc` | 使用NVIDIA硬件编码器 |
| `-preset ultrafast` | 编码速度预设（更快但文件更大）|
| `-tune zerolatency` | 优化低延迟 |
| `-b:v 3000k` | 视频比特率3000kbps |
| `-f flv` | 输出格式为FLV（RTMP需要）|

## 其他推流工具

### XSplit Broadcaster

1. 下载：[https://www.xsplit.com/](https://www.xsplit.com/)
2. 添加场景和来源
3. 设置 → 广播 → 添加自定义RTMP
4. 输入RTMP地址和流密钥
5. 开始广播

### vMix

1. 下载：[https://www.vmix.com/](https://www.vmix.com/)
2. 添加输入源
3. 设置 → 流设置
4. 选择自定义RTMP
5. 输入推流地址

## 推荐配置

### 低延迟配置（适合本地网络）

- **编码器**：x264 或硬件编码器
- **预设**：ultrafast / veryfast
- **比特率**：2500-3500 Kbps
- **关键帧间隔**：2秒（60帧时设置为60）
- **帧率**：30 FPS
- **分辨率**：1920x1080 或 1280x720

### 高质量配置（适合强大的网络和硬件）

- **编码器**：x264 或 NVENC
- **预设**：fast / medium
- **比特率**：5000-8000 Kbps
- **关键帧间隔**：2秒
- **帧率**：60 FPS
- **分辨率**：1920x1080

### 低性能设备配置

- **编码器**：硬件编码器（NVENC / QuickSync / AMF）
- **预设**：fast
- **比特率**：1500-2500 Kbps
- **关键帧间隔**：2秒
- **帧率**：30 FPS
- **分辨率**：1280x720

## 故障排查

### 无法连接到RTMP服务器

1. 确认SRS服务器正在运行
2. 检查防火墙设置，确保1935端口开放
3. 验证RTMP地址是否正确（包括协议、主机、端口、应用名）

### 推流断断续续

1. 降低比特率
2. 使用更快的编码预设（ultrafast）
3. 减小分辨率或帧率
4. 检查网络带宽

### 画面延迟高

1. 启用 `-tune zerolatency` 参数（FFmpeg）
2. 减小缓冲区大小
3. 使用更快的编码预设
4. 减小关键帧间隔

### OBS 推流失败

1. 检查推流地址和流密钥是否正确
2. 尝试重启 OBS
3. 查看 OBS 日志文件：`帮助` → `日志文件` → `查看当前日志`

## 查看推流

推流成功后，可以通过以下方式观看：

1. **YASS Web 播放器**：访问 `http://localhost:5001`
2. **VLC 播放器**：
   ```
   媒体 → 打开网络串流 → 输入 rtmp://localhost:1935/live/room_abc123_h264
   ```
3. **FFplay**：
   ```bash
   ffplay rtmp://localhost:1935/live/room_abc123_h264
   ```

## 注意事项

1. 确保使用**H.264编码器**，SRS服务器默认只支持H.264
2. 保持**关键帧间隔**在2秒左右，太长会影响播放器启动速度
3. 使用**CBR（固定比特率）**模式更稳定
4. 推流前确保SRS服务器已启动（Docker容器运行中）
5. 流密钥包含房间ID和编码格式后缀（如 `_h264`），不要修改

## 进阶技巧

### 多路推流

同时推送到多个平台：

```bash
# 使用 tee muxer 同时推流到两个服务器
ffmpeg -f gdigrab -i desktop -c:v libx264 -preset ultrafast \
  -f tee "[f=flv]rtmp://localhost:1935/live/room1|[f=flv]rtmp://remote:1935/live/room2"
```

### 添加音频

```bash
# Windows - 同时捕获桌面和音频
ffmpeg -f gdigrab -i desktop -f dshow -i audio="麦克风 (Realtek High Definition Audio)" \
  -c:v libx264 -preset ultrafast -c:a aac -b:a 128k \
  -f flv rtmp://localhost:1935/live/room_abc123_h264
```

### 叠加文字水印

```bash
ffmpeg -f gdigrab -i desktop -vf "drawtext=text='My Stream':x=10:y=10:fontsize=24:fontcolor=white" \
  -c:v libx264 -preset ultrafast -f flv rtmp://localhost:1935/live/room_abc123_h264
```

## 获取帮助

如有问题，请查看：
- [YASS GitHub Issues](https://github.com/yourrepo/YASS/issues)
- [OBS 官方论坛](https://obsproject.com/forum/)
- [FFmpeg 文档](https://ffmpeg.org/documentation.html)
