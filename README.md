# PhotoMind AI

基于 DeepSeek 视觉 AI 的本地照片智能识别、分类、审核和整理软件。

PhotoMind AI 是一款 Windows 桌面应用：它可以扫描电脑中的照片，使用 DeepSeek 视觉模型理解照片内容并给出分类建议，让你审核后决定是否整理（复制/移动/仅建目录）。**分类与整理完全分离**，扫描和分类阶段只读取原始照片，绝不修改、移动、重命名或删除原始文件。

> 当前进度：**Phase 0 + Phase 1** 已完成（项目骨架、基础 UI、图片扫描、缩略图、网格虚拟化、扫描记录持久化）。DeepSeek API 接入（Phase 2）及后续阶段尚未开始。

---

## 功能概览

### 已完成（Phase 0 + Phase 1）

- 分层解决方案（`PhotoMind.Core` / `PhotoMind.Infrastructure` / `PhotoMind.App`）。
- WPF 主界面：左侧导航栏 + 主工作区（首页 / 图片扫描 / 其余页面为占位）。
- 首页：显示已扫描图片数、未分类数、最近扫描任务等统计。
- 图片扫描：选择文件夹 / 磁盘 / 多目录、递归扫描、排除目录、格式筛选、进度显示、暂停/继续/取消。
- 图片类型识别：通过文件内容魔数判断格式（不依赖扩展名），阶段一支持 JPG/JPEG/PNG/WebP/BMP/GIF（TIFF/HEIC/RAW 可识别但暂不进入 AI 支持列表）。
- 缩略图：基于 ImageSharp 生成 256px JPEG 缩略图，按「路径+大小+修改时间」做缓存，绝不修改原图。
- 图片网格：WPF `ListBox` 虚拟化，按需加载缩略图，不为上万张图片一次性创建高分辨率 BitmapSource。
- 扫描记录持久化：SQLite（EF Core）保存扫描任务与图片元数据（文件名/路径/大小/尺寸/格式/SHA-256/EXIF 拍摄时间等）。
- 结构化日志：Serilog 写入本地日志目录。

### 规划中（Phase 2 及以后）

- DeepSeek API 接入与图片识别（API Key 安全存储、重试、限流、用量统计）。
- 分类体系管理、AI 自动/自定义/混合分类、分类审核。
- 照片整理（仅建目录 / 复制 / 移动，安全撤销）。
- 任务恢复、性能优化、重复图片检测、安装程序。

---

## 技术栈

| 模块 | 技术 |
|---|---|
| 语言 / 框架 | C# / WPF / .NET 8 |
| UI 架构 | MVVM（CommunityToolkit.Mvvm） |
| 数据库 | SQLite + EF Core |
| 图片处理 | SixLabors.ImageSharp 3.x |
| 日志 | Serilog |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |
| HTTP / JSON | HttpClient / System.Text.Json |

---

## 环境要求

- Windows 10 22H2+ / Windows 11。
- .NET 8 SDK（本仓库使用 8.0.428，见 `global.json`）。
- 本项目已内置本地 .NET SDK（`.dotnet/`）与离线 NuGet 包源（`.nuget/offline/`），可在离线环境下构建。

---

## 项目结构

```
PhotoMindAI/
├── PhotoMindAI.sln
├── Directory.Build.props
├── global.json
├── nuget.config            # 在线源（默认）
├── nuget.offline.config    # 离线源（离线构建用）
├── src/
│   ├── PhotoMind.Core/           # 领域模型、枚举、DTO、接口、常量
│   ├── PhotoMind.Infrastructure/ # 扫描/图片处理/数据库/AI(待)/日志
│   └── PhotoMind.App/            # WPF 应用（Views/ViewModels/Converters/Styles）
├── docs/
│   └── ARCHITECTURE.md
└── scripts/
    ├── build.ps1
    ├── test.ps1
    └── publish.ps1
```

---

## 构建

> 本机 .NET 的 Schannel 无法访问 `https://api.nuget.org`（TLS 栈差异），因此使用**离线包源**构建。所有依赖已预下载到 `.nuget/offline/`。

直接运行：

```powershell
.\scripts\build.ps1
```

或手动执行等价命令（注意必须先设置环境变量，否则 dotnet 首次运行哨兵与用户级 NuGet 配置会尝试写入工作区之外）：

```powershell
$root = "D:\AI_Agent+\dsh_next\PhotoMindAI"
$env:DOTNET_CLI_HOME = "$root\.dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
$env:APPDATA = "$root\.home\AppData\Roaming"
$env:LOCALAPPDATA = "$root\.home\AppData\Local"
$env:TEMP = "$root\.home\Temp"; $env:TMP = $env:TEMP
$env:NUGET_PACKAGES = "$root\.nuget\packages"

& "$root\.dotnet\dotnet.exe" msbuild "$root\PhotoMindAI.sln" /t:Build `
  /p:RestoreConfigFile=nuget.offline.config `
  /p:MSBuildEnableWorkloadResolver=false `
  -m:1 -nodeReuse:false -v:minimal
```

构建产物：`src/PhotoMind.App/bin/Debug/net8.0-windows/PhotoMind.App.exe`。

---

## 运行

框架依赖模式运行（需指向本地运行时）：

```powershell
$root = "D:\AI_Agent+\dsh_next\PhotoMindAI"
$env:DOTNET_ROOT = "$root\.dotnet"
$env:PHOTOMIND_DATA_DIR = "$root\.testdata"   # 可选：指定数据目录，默认 %LOCALAPPDATA%\PhotoMind AI
& "$root\src\PhotoMind.App\bin\Debug\net8.0-windows\PhotoMind.App.exe"
```

数据目录（可配置）包含：`photomin.db`（SQLite 数据库）、`thumbnails/`（缩略图缓存）、`logs/`（日志）。

---

## DeepSeek API Key 配置（Phase 2 接入后生效）

- API Key 由用户在设置页自行填写，使用 Windows DPAPI / Credential Manager 加密存储，**绝不**硬编码、写入 Git、写入日志或普通配置文件。
- 默认接口：`https://api.deepseek.com`，默认模型：`deepseek-flash`（均可修改）。
- 未配置 API Key 时，本软件仍可正常扫描与浏览照片，仅 AI 分类功能不可用。

---

## 安全与隐私原则

1. 扫描和分类阶段只读原始图片，禁止修改、移动、重命名、删除原始文件。
2. AI 只提供分类建议，所有文件系统操作由 C# 程序执行。
3. 分类与整理完全分离；完成分类不代表自动移动照片。
4. 任何物理文件操作必须经过用户主动确认（默认复制，禁止默认移动）。
5. 扫描记录、分类结果、用户规则、缩略图缓存与日志全部保存在本地。
6. 默认不上传原图；仅用户主动启动 AI 分类后才发送必要的图片预览，并明确告知。

---

## 文档

- [架构文档](docs/ARCHITECTURE.md)
