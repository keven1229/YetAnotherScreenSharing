# YASS 本地开发启动脚本
# 使用方法: .\scripts\start-dev.ps1 [-SkipDocker] [-ApiOnly] [-WebOnly] [-DesktopOnly]

param(
    [switch]$SkipDocker,      # 跳过启动 Docker SRS
    [switch]$ApiOnly,          # 只启动 API
    [switch]$WebOnly,          # 只启动 Web 前端
    [switch]$DesktopOnly,      # 只启动桌面客户端
    [switch]$All               # 启动所有服务
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  YASS - 本地开发环境启动脚本" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 检查 .NET SDK
Write-Host "[检查] .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[错误] 未找到 .NET SDK，请先安装 .NET 8 SDK" -ForegroundColor Red
    exit 1
}
Write-Host "[√] .NET SDK 版本: $dotnetVersion" -ForegroundColor Green

# 启动 Docker SRS (流媒体服务器)
if (-not $SkipDocker) {
    Write-Host ""
    Write-Host "[启动] SRS 流媒体服务器 (Docker)..." -ForegroundColor Yellow
    
    # 检查 Docker 是否运行
    $dockerRunning = docker info 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[警告] Docker 未运行，跳过 SRS 启动" -ForegroundColor DarkYellow
        Write-Host "       请手动启动 Docker Desktop" -ForegroundColor DarkYellow
    } else {
        Push-Location "$projectRoot\deploy"
        docker-compose up -d
        Pop-Location
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[√] SRS 已启动" -ForegroundColor Green
            Write-Host "    - RTMP: rtmp://localhost:1935" -ForegroundColor Gray
            Write-Host "    - HTTP-FLV: http://localhost:8080" -ForegroundColor Gray
            Write-Host "    - API: http://localhost:1985" -ForegroundColor Gray
        }
    }
}

# 构建解决方案
Write-Host ""
Write-Host "[构建] 编译整个解决方案..." -ForegroundColor Yellow
Push-Location $projectRoot
dotnet build YASS.sln --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "[错误] 构建失败" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host "[√] 构建成功" -ForegroundColor Green

# 启动服务
$jobs = @()

# 启动 API
if (-not $WebOnly -and -not $DesktopOnly) {
    Write-Host ""
    Write-Host "[启动] YASS.Server.Api (http://localhost:5000)..." -ForegroundColor Yellow
    $apiJob = Start-Job -ScriptBlock {
        param($projectRoot)
        Set-Location "$projectRoot\src\YASS.Server.Api\YASS.Server.Api"
        dotnet run --no-build --urls "http://localhost:5000"
    } -ArgumentList $projectRoot
    $jobs += @{ Name = "API"; Job = $apiJob }
    Start-Sleep -Seconds 2
    Write-Host "[√] API 服务已启动" -ForegroundColor Green
}

# 启动 Web
if ($WebOnly -or $All) {
    Write-Host ""
    Write-Host "[启动] YASS.Web (http://localhost:5001)..." -ForegroundColor Yellow
    $webJob = Start-Job -ScriptBlock {
        param($projectRoot)
        Set-Location "$projectRoot\src\YASS.Web\YASS.Web"
        dotnet run --no-build --urls "http://localhost:5001"
    } -ArgumentList $projectRoot
    $jobs += @{ Name = "Web"; Job = $webJob }
    Start-Sleep -Seconds 2
    Write-Host "[√] Web 前端已启动" -ForegroundColor Green
}

# 启动桌面客户端
if ($DesktopOnly -or $All -or (-not $WebOnly -and -not $ApiOnly)) {
    Write-Host ""
    Write-Host "[启动] YASS.Client.Desktop..." -ForegroundColor Yellow
    Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "$projectRoot\src\YASS.Client.Desktop\YASS.Client.Desktop\YASS.Client.Desktop.csproj", "--no-build"
    Write-Host "[√] 桌面客户端已启动" -ForegroundColor Green
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  所有服务已启动！" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "服务地址:" -ForegroundColor White
Write-Host "  - API:     http://localhost:5000" -ForegroundColor Gray
Write-Host "  - Web:     http://localhost:5001" -ForegroundColor Gray
Write-Host "  - SRS:     rtmp://localhost:1935" -ForegroundColor Gray
Write-Host ""
Write-Host "按 Ctrl+C 停止所有服务..." -ForegroundColor Yellow

# 等待用户中断
try {
    while ($true) {
        foreach ($j in $jobs) {
            $output = Receive-Job -Job $j.Job -Keep 2>$null
            if ($output) {
                Write-Host "[$($j.Name)] $output"
            }
        }
        Start-Sleep -Seconds 1
    }
} finally {
    Write-Host ""
    Write-Host "[停止] 正在停止所有服务..." -ForegroundColor Yellow
    foreach ($j in $jobs) {
        Stop-Job -Job $j.Job -ErrorAction SilentlyContinue
        Remove-Job -Job $j.Job -Force -ErrorAction SilentlyContinue
    }
    Write-Host "[√] 所有服务已停止" -ForegroundColor Green
}
