# YASS 部署指南

本文档介绍如何在服务器上部署 YASS (Yet Another Screen Sharing) 系统。

## 📋 服务组件概览

| 组件 | 描述 | 默认端口 |
|------|------|----------|
| **YASS.Server.Api** | REST API 服务器 | 5000 |
| **SRS** | 流媒体服务器 (RTMP/HTTP-FLV/HLS) | 1935, 8080, 1985 |
| **YASS.Web.React** | React 前端 (静态文件) | 3000 (开发) |
| **YASS.Web** | Blazor WebAssembly 前端 | 5001 (开发) |
| **YASS.Client.Desktop** | Windows 桌面推流客户端 | - |

---

## 🔧 配置文件位置

### API 服务器

| 文件 | 用途 |
|------|------|
| `src/YASS.Server.Api/appsettings.json` | 默认配置 |
| `src/YASS.Server.Api/appsettings.Development.json` | 开发环境配置 |
| `src/YASS.Server.Api/appsettings.Production.json` | 生产环境配置 |

**配置项说明：**

```json
{
  "Srs": {
    "RtmpServer": "rtmp://localhost:1935",      // SRS RTMP 服务器地址（用于生成推流地址）
    "HttpFlvServer": "http://localhost:8080",   // SRS HTTP-FLV 服务器地址（用于生成播放地址）
    "HlsServer": "http://localhost:8080",       // SRS HLS 服务器地址
    "AppName": "live",                          // 流应用名称
    "H264Suffix": "_h264"                       // H.264 转码流后缀
  }
}
```

### 桌面客户端

| 文件 | 用途 |
|------|------|
| `src/YASS.Client.Desktop/appsettings.json` | 客户端配置 |

```json
{
  "ApiBaseAddress": "http://localhost:5000"   // API 服务器地址
}
```

### Blazor Web 前端

| 文件 | 用途 |
|------|------|
| `src/YASS.Web/wwwroot/appsettings.json` | 默认配置 |
| `src/YASS.Web/wwwroot/appsettings.Production.json` | 生产环境配置 |

```json
{
  "ApiBaseAddress": "http://localhost:5000"   // API 服务器地址
}
```

### React Web 前端

| 文件 | 用途 |
|------|------|
| `src/YASS.Web.React/.env.development` | 开发环境变量 |
| `src/YASS.Web.React/.env.production` | 生产环境变量 |

```bash
VITE_API_URL=http://localhost:5000   # API 服务器地址
```

### SRS 流媒体服务器

| 文件 | 用途 |
|------|------|
| `deploy/srs/srs.conf` | SRS 配置文件 |

---

## 🚀 服务器部署步骤

假设服务器 IP/域名为 `your-server.com`。

### 1. 部署 SRS 流媒体服务器

#### 修改 SRS 配置

编辑 `deploy/srs/srs.conf`，修改回调地址：

```conf
http_hooks {
    enabled         on;
    on_connect      http://your-server.com:5000/api/srs/on_connect;
    on_publish      http://your-server.com:5000/api/srs/on_publish;
    on_unpublish    http://your-server.com:5000/api/srs/on_unpublish;
    on_play         http://your-server.com:5000/api/srs/on_play;
    on_stop         http://your-server.com:5000/api/srs/on_stop;
}
```

> **注意**：如果 SRS 和 API 在同一台服务器的 Docker 网络中，可使用 `http://host.docker.internal:5000` 或容器名 `http://api:5000`。

#### 启动 SRS

```bash
cd deploy
docker-compose up -d
```

验证 SRS 运行状态：
```bash
# 检查容器状态
docker ps

# 查看 SRS 日志
docker logs yass-srs

# 访问 SRS 控制台
curl http://localhost:1985/api/v1/summaries
```

### 2. 部署 API 服务器

#### 修改生产环境配置

编辑 `src/YASS.Server.Api/appsettings.Production.json`：

```json
{
  "Srs": {
    // RTMP 必须保留端口（无法通过 HTTP 反代）
    "RtmpServer": "rtmp://your-server.com:1935",
    // HTTP 服务通过 Nginx 反代，使用统一入口（无端口）
    "HttpFlvServer": "https://your-server.com",
    "HlsServer": "https://your-server.com",
    "AppName": "live",
    "H264Suffix": "_h264"
  }
}
```

#### 发布并运行

```bash
# 发布
cd src/YASS.Server.Api
dotnet publish -c Release -o /app/yass-api

# 运行
cd /app/yass-api
ASPNETCORE_ENVIRONMENT=Production \
ASPNETCORE_URLS="http://0.0.0.0:5000" \
dotnet YASS.Server.Api.dll
```

#### 使用环境变量覆盖配置

也可以通过环境变量覆盖配置：

```bash
export Srs__RtmpServer="rtmp://your-server.com:1935"
export Srs__HttpFlvServer="https://your-server.com"   # 通过 Nginx 反代
export Srs__HlsServer="https://your-server.com"        # 通过 Nginx 反代
```

#### 使用 systemd 管理服务 (Linux)

创建 `/etc/systemd/system/yass-api.service`：

```ini
[Unit]
Description=YASS API Server
After=network.target

[Service]
Type=notify
User=www-data
WorkingDirectory=/app/yass-api
ExecStart=/usr/bin/dotnet /app/yass-api/YASS.Server.Api.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable yass-api
sudo systemctl start yass-api
```

### 3. 部署 React 前端

#### 修改生产环境配置

编辑 `src/YASS.Web.React/.env.production`：

```bash
VITE_API_URL=http://your-server.com:5000
```

#### 构建静态文件

```bash
cd src/YASS.Web.React
npm install
npm run build
```

构建产物在 `dist/` 目录。

#### 部署到 Nginx

```bash
# 复制静态文件
sudo cp -r dist/* /var/www/yass/

# Nginx 配置示例
sudo nano /etc/nginx/sites-available/yass
```

Nginx 配置示例：

```nginx
server {
    listen 80;
    server_name your-server.com;
    root /var/www/yass;
    index index.html;

    # React SPA 路由支持
    location / {
        try_files $uri $uri/ /index.html;
    }

    # API 代理（可选，如果前端和 API 使用同一域名）
    location /api {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/yass /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### 4. 配置桌面客户端

修改 `appsettings.json`（与 exe 同目录）：

```json
{
  "ApiBaseAddress": "http://your-server.com:5000"
}
```

---

## 🔌 端口与网络架构

### 端口暴露策略

| 端口 | 服务 | 对外暴露 | 说明 |
|------|------|:--------:|------|
| **80/443** | Nginx | ✅ 必须 | 统一 HTTP/HTTPS 入口 |
| **1935** | SRS RTMP | ✅ 必须 | 推流端口，桌面客户端直连 |
| 5000 | API Server | ❌ 内部 | 通过 Nginx 反代 |
| 8080 | SRS HTTP-FLV/HLS | ❌ 内部 | 通过 Nginx 反代 |
| 1985 | SRS API | ❌ 内部 | 仅管理/调试用 |

> **结论**：只需对外暴露 **80/443**（HTTP/HTTPS）和 **1935**（RTMP）两个端口！

### 为什么 RTMP 必须单独暴露？

RTMP 是基于 TCP 的二进制协议，不是 HTTP，无法通过标准 Nginx HTTP 反代。有两种处理方式：

1. **直接暴露 1935**（推荐）：简单，性能最好
2. **Nginx Stream 模块**：可以做 TCP 层代理，但无实际收益

---

## 🌐 Nginx 统一反代配置

所有 HTTP 服务通过 Nginx 统一入口，推荐的 URL 路径规划：

| 路径 | 后端服务 | 用途 |
|------|----------|------|
| `/` | 静态文件 | React 前端 |
| `/api/*` | API Server (5000) | REST API |
| `/live/*.flv` | SRS (8080) | HTTP-FLV 播放 |
| `/live/*.m3u8` | SRS (8080) | HLS 播放 |

### 完整 Nginx 配置

```nginx
upstream api_server {
    server 127.0.0.1:5000;
    keepalive 32;
}

upstream srs_server {
    server 127.0.0.1:8080;
    keepalive 32;
}

server {
    listen 80;
    server_name your-server.com;
    
    # 可选：重定向到 HTTPS
    # return 301 https://$host$request_uri;

    root /var/www/yass;
    index index.html;

    # ========== React 前端 ==========
    location / {
        try_files $uri $uri/ /index.html;
    }

    # ========== API 反代 ==========
    location /api {
        proxy_pass http://api_server;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # 支持长连接
        proxy_set_header Connection "";
        proxy_connect_timeout 60s;
        proxy_read_timeout 300s;
    }

    # ========== HTTP-FLV 播放（低延迟） ==========
    location /live/ {
        proxy_pass http://srs_server;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        
        # 关键：FLV 流式传输需要关闭缓冲
        proxy_buffering off;
        proxy_cache off;
        
        # 流媒体需要长连接
        proxy_connect_timeout 60s;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
        
        # 支持 chunked 传输
        chunked_transfer_encoding on;
    }

    # ========== HLS 播放 ==========
    location ~ \.m3u8$ {
        proxy_pass http://srs_server;
        proxy_set_header Host $host;
        
        # HLS 允许缓存
        proxy_cache_valid 200 1s;
    }

    location ~ \.ts$ {
        proxy_pass http://srs_server;
        proxy_set_header Host $host;
        
        # TS 分片缓存
        proxy_cache_valid 200 10m;
    }
}

# ========== HTTPS 配置（可选） ==========
server {
    listen 443 ssl http2;
    server_name your-server.com;
    
    ssl_certificate /etc/nginx/ssl/your-server.com.crt;
    ssl_certificate_key /etc/nginx/ssl/your-server.com.key;
    
    # ... 其他配置与上面 HTTP 相同 ...
}
```

### 使用统一入口后的地址变化

| 服务 | 直接访问 | 通过 Nginx |
|------|----------|------------|
| API | `http://server:5000/api/rooms` | `http://server/api/rooms` |
| HTTP-FLV | `http://server:8080/live/xxx.flv` | `http://server/live/xxx.flv` |
| HLS | `http://server:8080/live/xxx.m3u8` | `http://server/live/xxx.m3u8` |
| RTMP | `rtmp://server:1935/live/xxx` | **不变（无法反代）** |

---

## 📝 配置文件调整

使用 Nginx 统一入口后，需要更新以下配置：

### API 服务器 SRS 配置

`src/YASS.Server.Api/appsettings.Production.json`：

```json
{
  "Srs": {
    "RtmpServer": "rtmp://your-server.com:1935",
    "HttpFlvServer": "http://your-server.com",      // 去掉 :8080
    "HlsServer": "http://your-server.com",          // 去掉 :8080
    "AppName": "live",
    "H264Suffix": "_h264"
  }
}
```

### React 前端

`src/YASS.Web.React/.env.production`：

```bash
VITE_API_URL=http://your-server.com    # 去掉 :5000
```

### 桌面客户端

`appsettings.json`：

```json
{
  "ApiBaseAddress": "http://your-server.com"    // 去掉 :5000
}
```

---

## 🏗️ 网络架构图

```
                    ┌─────────────────────────────────────────┐
                    │              Internet                    │
                    └─────────────────────────────────────────┘
                                      │
                    ┌─────────────────┴─────────────────┐
                    │                                    │
              ┌─────▼─────┐                        ┌─────▼─────┐
              │  :80/443  │                        │   :1935   │
              │   Nginx   │                        │  SRS RTMP │
              │  (HTTP)   │                        │   (TCP)   │
              └─────┬─────┘                        └───────────┘
                    │                                    │
        ┌───────────┼───────────┐                       │
        │           │           │                       │
   ┌────▼────┐ ┌────▼────┐ ┌────▼────┐                 │
   │   /     │ │  /api   │ │ /live/  │                 │
   │ Static  │ │  :5000  │ │  :8080  │                 │
   │  Files  │ │   API   │ │SRS HTTP │◄────────────────┘
   └─────────┘ └─────────┘ └─────────┘               (同一进程)
                                                       
   ─────────────────────────────────────────────────────
                    │ 仅本机 (127.0.0.1)  │
                    │   无需对外暴露端口  │
   ─────────────────────────────────────────────────────
```

---

## 🔥 最小化防火墙配置

只需开放两个端口：

```bash
# UFW (Ubuntu)
sudo ufw allow 80/tcp      # HTTP
sudo ufw allow 443/tcp     # HTTPS (可选)
sudo ufw allow 1935/tcp    # RTMP 推流

# firewalld (CentOS/RHEL)
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --permanent --add-port=1935/tcp
sudo firewall-cmd --reload
```

确保防火墙开放这些端口：

```bash
# UFW (Ubuntu)
sudo ufw allow 5000/tcp
sudo ufw allow 1935/tcp
sudo ufw allow 8080/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# firewalld (CentOS/RHEL)
sudo firewall-cmd --permanent --add-port=5000/tcp
sudo firewall-cmd --permanent --add-port=1935/tcp
sudo firewall-cmd --permanent --add-port=8080/tcp
sudo firewall-cmd --reload
```

---

## 🐳 Docker Compose 完整部署

编辑 `deploy/docker-compose.yml`，取消注释 API 服务部分并修改环境变量：

```yaml
version: '3.8'

services:
  srs:
    build:
      context: ./srs
      dockerfile: Dockerfile
    image: yass-srs:v6
    container_name: yass-srs
    ports:
      - "1935:1935"
      - "8080:8080"
      - "1985:1985"
    volumes:
      - ./srs/srs.conf:/usr/local/srs/conf/srs.conf:ro
    restart: unless-stopped
    networks:
      - yass-network

  api:
    build:
      context: ../src/YASS.Server.Api
      dockerfile: Dockerfile
    image: yass-api:latest
    container_name: yass-api
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Srs__RtmpServer=rtmp://your-server.com:1935
      - Srs__HttpFlvServer=https://your-server.com
      - Srs__HlsServer=https://your-server.com
    depends_on:
      - srs
    restart: unless-stopped
    networks:
      - yass-network

networks:
  yass-network:
    driver: bridge
```

启动所有服务：

```bash
cd deploy
docker-compose up -d --build
```

---

## ✅ 验证部署

### 1. 检查 API 服务

```bash
curl http://your-server.com:5000/api/rooms
# 应返回: {"success":true,"data":{"rooms":[],...}}
```

### 2. 检查 SRS 服务

```bash
curl http://your-server.com:1985/api/v1/summaries
# 应返回 SRS 状态信息
```

### 3. 检查前端

浏览器访问 `http://your-server.com`，应看到房间列表页面。

### 4. 测试推流

使用桌面客户端或 FFmpeg 测试推流：

```bash
ffmpeg -re -i test.mp4 -c copy -f flv rtmp://your-server.com:1935/live/test
```

### 5. 测试播放

浏览器访问 `http://your-server.com:8080/live/test.flv` 或通过前端观看。

---

## 🔒 安全建议

1. **使用 HTTPS**：配置 SSL 证书，通过 Nginx 反向代理启用 HTTPS
2. **限制 API 访问**：考虑添加 API 认证
3. **防火墙**：仅开放必要端口
4. **SRS 鉴权**：启用 SRS 推流/播放鉴权（已通过 HTTP 回调实现）

---

## 📝 常见问题

### Q: 播放时显示黑屏

检查 SRS 是否正常运行，确认播放地址格式正确：
- HTTP-FLV: `http://your-server.com:8080/live/{streamKey}.flv`
- HLS: `http://your-server.com:8080/live/{streamKey}.m3u8`

### Q: 推流失败

1. 检查防火墙是否开放 1935 端口
2. 检查 SRS 回调地址是否正确配置
3. 查看 API 服务器日志确认鉴权状态

### Q: 前端无法连接 API

1. 确认 CORS 配置允许前端域名
2. 检查 `VITE_API_URL` 配置是否正确
3. 确认 API 服务器正在运行
