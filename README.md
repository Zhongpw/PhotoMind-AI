# PhotoMind AI

基于 DeepSeek 视觉 AI 的本地照片智能识别、分类、审核和整理软件。

PhotoMind AI 是一款 Windows 桌面应用：它可以扫描电脑中的照片，使用 DeepSeek 视觉模型理解照片内容并给出分类建议，让你审核后决定是否整理（复制/移动/仅建目录）。**分类与整理完全分离**，扫描和分类阶段只读取原始照片，绝不修改、移动、重命名或删除原始文件。

> 当前进度：**全部 6 个阶段已完成，并完成 4 项增强**（Phase 0 项目骨架 → Phase 1 图片扫描 → Phase 2 DeepSeek API 接入 → Phase 3 分类体系与审核 → Phase 4 照片整理 → Phase 5 任务恢复与优化 → Phase 6 最终发布 → 增强：xUnit/Moq 单元测试、EF Core 迁移、TIFF/HEIC/RAW 支持、Inno Setup 正式安装包）。

---

## 功能概览

### 核心功能（全部 6 阶段 + 4 项增强已完成）

- **图片扫描**：选择文件夹 / 磁盘 / 多目录、递归扫描、排除目录、格式筛选、暂停/继续/取消；文件内容魔数识别格式（不依赖扩展名）；256px 缩略图缓存；网格虚拟化。
- **AI 智能分类**（DeepSeek 视觉）：三种模式（AI 自动 / 用户自定义 / 混合）、API Key DPAPI 加密存储、图片预处理（缩放压缩，不上传原图）、指数退避重试、用量统计、暂停/恢复/取消。
- **分类审核**：按分类/置信度/待审核/关键词筛选，单张与批量修改分类。
- **照片整理**：按分类建文件夹、仅建目录 / 复制（默认）/ 移动，安全校验（SHA-256、防覆盖、防路径穿越、磁盘空间检查）、同名自动改名、完整撤销。
- **任务恢复与优化**：异常退出自动恢复中断任务、重复图片检测、API 用量统计、历史任务页、缩略图并发限制、10k 张性能验证。
- **最终发布**：自包含 x64 发布、安装/卸载脚本、完整文档。
- **增强**：xUnit/Moq 单元测试（100 例）、EF Core 迁移（数据库结构版本管理）、TIFF/HEIC/RAW 本地解码（Magick.NET）、Inno Setup 正式安装包（`installer/PhotoMindAI-Setup-1.0.0.exe`，51.6 MB）。

### 技术要点

- 分层解决方案（`PhotoMind.Core` / `PhotoMind.Infrastructure` / `PhotoMind.App`，MVVM）。
- SQLite（EF Core）+ WAL，9 张表；结构化日志（Serilog）。
- 安全优先：扫描/分类只读原图，AI 只给建议，文件操作由用户确认，分类与整理分离，本地数据优先。

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
│   ├── ARCHITECTURE.md
│   ├── DATABASE.md
│   ├── DEEPSEEK_API.md
│   ├── USER_GUIDE.md
│   ├── TEST_PLAN.md
│   ├── THIRD_PARTY_NOTICES.md
│   └── CHANGELOG.md
└── scripts/
    ├── build.ps1
    ├── test.ps1
    ├── publish.ps1
    ├── install.ps1
    └── uninstall.ps1
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
- [数据库设计](docs/DATABASE.md)
- [DeepSeek API 接入说明](docs/DEEPSEEK_API.md)
- [用户指南](docs/USER_GUIDE.md)
- [测试计划](docs/TEST_PLAN.md)
- [第三方许可声明](docs/THIRD_PARTY_NOTICES.md)
- [更新日志](docs/CHANGELOG.md)
