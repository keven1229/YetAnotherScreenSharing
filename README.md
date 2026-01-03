# YASS - Yet Another Screen Sharing

多人实时屏幕共享应用，基于 SRS v6+ 流媒体服务器。

## 项目结构

```
YetAnotherScreenSharing/
├── src/
│   ├── YASS.Shared/              # 共享DTO、枚举、接口契约
│   ├── YASS.Server.Api/          # ASP.NET Core 后端（房间管理、鉴权接口）
│   ├── YASS.Client.Core/         # 客户端核心库（屏幕捕获、编解码、推拉流）
│   ├── YASS.Client.Desktop/      # WPF + WPF UI 桌面客户端
│   └── YASS.Web/                 # Blazor WASM 观看端
├── deploy/
│   ├── srs/                      # SRS v6 配置
│   └── docker-compose.yml        # 部署编排
└── docs/                         # 文档
```

## 技术栈

- **推流客户端**: C# / WPF / WPF UI (Fluent Design) / FFmpeg (Sdcb.FFmpeg)
- **观看客户端**: C# / WPF / FFmpeg 或 Blazor WASM / flv.js
- **后端 API**: ASP.NET Core 9.0
- **流媒体服务器**: SRS v6+ (Enhanced RTMP, HEVC 支持)
- **编码**: H.265/HEVC (推流), H.264 (Web 兼容转码)
- **传输协议**: RTMP (推流), HTTP-FLV (拉流)

## 快速开始

### 1. 启动 SRS 服务器

```bash
cd deploy
docker-compose up -d
```

### 2. 运行后端 API

```bash
cd src/YASS.Server.Api/YASS.Server.Api
dotnet run
```

### 3. 运行桌面客户端

```bash
cd src/YASS.Client.Desktop/YASS.Client.Desktop
dotnet run
```

### 4. 运行 Web 端

```bash
cd src/YASS.Web/YASS.Web
dotnet run
```

## 功能特性

- [x] 多房间支持
- [x] H.265/HEVC 高效编码
- [x] 硬件编码支持 (NVENC/AMF/QSV)
- [x] 灵活的手动配置选项
- [ ] 权限控制 (预留 Keycloak OIDC 接口)
- [ ] 录制功能

## License

MIT
