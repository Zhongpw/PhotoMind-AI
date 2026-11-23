# PhotoMind AI — 架构文档

## 1. 总体架构

采用经典分层架构，职责单向依赖：

```
PhotoMind.App (WPF UI / MVVM)
        │  依赖
        ▼
PhotoMind.Infrastructure (业务实现 / 基础设施)
        │  依赖
        ▼
PhotoMind.Core (领域模型 / 接口 / 常量，无外部依赖)
```

依赖方向严格自上而下：`App → Infrastructure → Core`。`Core` 不引用任何具体框架或基础设施，`Infrastructure` 不引用 WPF。

## 2. 项目职责

### PhotoMind.Core

| 目录 | 内容 |
|---|---|
| `Models/` | 领域实体：`Image`、`Category`、`ClassificationResult`、`ScanTask`、`ClassificationTask`、`OrganizeTask`、`OrganizeLog`、`UserRule`、`ApiUsage` |
| `Enums/` | `ImageFormat`、`ImageScanStatus`、`TaskState`、`ClassificationMode`、`OrganizeOperationType` |
| `DTOs/` | `ImageFileInfo`、`ScanOptions`、`ScanError`、`ScanProgress` |
| `Interfaces/` | `IAppPaths`、`IScanService`、`IScanRepository`、`IFileHashService`、`IImageFormatDetector`、`IImageMetadataReader`、`IThumbnailService` |
| `Constants/` | `AppConstants`、`AiConstants` |
| `Threading/` | `PauseToken`（任务暂停/继续的协作式令牌） |
| `Exceptions/` | `PhotoMindException` |

### PhotoMind.Infrastructure

| 目录 | 内容 |
|---|---|
| `Services/` | `AppPaths`（数据目录解析）、`ScanService`（扫描编排） |
| `FileSystem/` | `DirectoryScanner`（异步目录枚举、保护目录/排除/符号链接过滤、预估计数） |
| `ImageProcessing/` | `FileHashService`（SHA-256）、`ImageFormatDetector`（魔数判型）、`ImageMetadataReader`（尺寸/EXIF）、`ThumbnailService`（缩略图生成与缓存） |
| `Database/` | `AppDbContext`（EF Core）、`DatabaseInitializer`（自动建库）、`ScanRepository`（扫描持久化） |
| `Logging/` | `LoggingConfig`（Serilog 文件日志） |
| `AI/` | （Phase 2 加入：`DeepSeekClient` 等） |
| `DependencyInjection.cs` | 服务注册入口 |

### PhotoMind.App

| 目录 | 内容 |
|---|---|
| `Views/` | `MainWindow`、`HomeView`、`ScanView`、`PlaceholderView` |
| `ViewModels/` | `MainViewModel`、`HomeViewModel`、`ScanViewModel`、`ImageItemViewModel`、`FormatFilterItem`、`NavigationItem`、`PlaceholderViewModel` |
| `Converters/` | `BooleanToVisibilityConverter`、`InverseBoolConverter`、`NullToCollapsedConverter`、`PathToImageSourceConverter` |
| `Styles/` | `Styles.xaml`（全局样式与资源字典） |
| `App.xaml(.cs)` | 启动装配（DI 容器、日志、数据库初始化、全局异常处理） |

## 3. 启动流程（App.xaml.cs `OnStartup`）

1. 构建 `ServiceCollection`，调用 `AddPhotoMindInfrastructure()` 注册基础设施服务，注册各 `ViewModel` 与 `MainWindow` 为单例。
2. `IAppPaths.EnsureCreated()` 创建数据目录（数据库/缩略图/日志目录）。
3. `LoggingConfig.Configure(paths)` 初始化 Serilog。
4. `DatabaseInitializer.InitializeAsync(dbFactory)` 创建 SQLite 数据库结构（当前用 `EnsureCreated`，Phase 2 起切换 EF Core Migration）。
5. `MainWindow.Show()`；并挂接 `DispatcherUnhandledException` / `UnhandledException` 全局异常处理。

## 4. 扫描数据流（Phase 1 核心）

```
ScanViewModel ──ScanAsync(options, PauseToken, IProgress<ScanProgress>, CancellationToken)──▶ ScanService
     │                                                                                          │
     │ 进度事件 / IProgress<ScanProgress>                                                       ▼
     │                                                                                 DirectoryScanner
     │                                                                               （异步枚举候选文件，
     │                                                                                过滤保护目录/排除目录/
     │                                                                                无权限/符号链接）
     │                                                                                          │
     │                                                                                          ▼
     │                                                                             逐文件 ProcessFile：
     │                                                                               1) 读前 16 字节 → ImageFormatDetector 判格式
     │                                                                               2) FileHashService 算 SHA-256
     │                                                                               3) ImageMetadataReader 读尺寸/EXIF
     │                                                                               4) ThumbnailService 生成缩略图
     │                                                                               5) 构造 Image 实体，标记 ScanStatus
     │                                                                                          │
     │                                                                                          ▼
     │                                                                              ScanRepository（EF Core）
     │                                                                           每 100 条批量写库；每 20 条报进度
     ◀──────────────────────────────────────────────────────────────────────────────────────────┘
```

关键约束：

- 全程**异步**（异步枚举目录 + 异步 IO + 异步数据库写入），不阻塞 UI 线程。
- 单个损坏/不支持文件标记为 `Failed`，绝不终止整个扫描任务。
- 扫描**只读**原始图片；缩略图写入独立缓存目录，绝不触碰原图。
- 每 100 条批量提交，避免每张图片一次数据库往返；错误最多记录 500 条。
- 暂停（`PauseToken`）/ 取消（`CancellationToken`）均被协作式响应；结束（完成/取消/失败）统一写回任务状态。

## 5. 关键设计决策与理由

| 决策 | 理由 |
|---|---|
| 格式判定用文件魔数而非扩展名 | 扩展名可伪造；魔数保证真实内容类型，且不依赖文件扩展名做无依据推断 |
| `ImageSharp` 生成缩略图时 `AutoOrient` + `ResizeMode.Max` | 正确处理 EXIF 方向，保持宽高比，控制输出尺寸 |
| 缩略图缓存键 = 路径 + 大小 + 修改时间 的 SHA-256 | 文件内容变化后自动重新生成缓存；不影响原图 |
| `IDbContextFactory` 短生命周期 context | 扫描是长时间后台任务，短生命周期 DbContext 避免长事务与内存增长，符合异步并发访问 |
| 图片网格用 `ListBox` 虚拟化 + 按需加载缩略图 | 面向 1,000～10,000 张图片，不得一次性创建高分辨率 BitmapSource |
| `EnsureCreated`（Phase 1） | Phase 0/1 快速建立可运行骨架；Phase 2 起切换为正式 EF Core Migration |
| 数据目录优先级：`PHOTOMIND_DATA_DIR` 环境变量 > `%LOCALAPPDATA%\PhotoMind AI` | 便于测试隔离，也让用户数据集中在一个可备份目录 |

## 6. 数据库（Phase 1 现状）

当前已建模并建表（详见 `AppDbContext.OnModelCreating`）：`Images`、`Categories`、`ClassificationResults`、`ScanTasks`、`ClassificationTasks`、`OrganizeTasks`、`OrganizeLogs`、`UserRules`、`ApiUsage`。

Phase 1 实际落库的是 `ScanTasks` 与 `Images`（扫描任务与图片元数据）。常用查询字段（`Images.OriginalPath` / `FileHash` / `ScanTaskId`，`ScanTasks.StartedAt` 等）已建立索引。

## 7. 已计划但未实现（Phase 2+）

- `Infrastructure/AI/`：`DeepSeekClient`、`DeepSeekOptions`、`PromptBuilder`、`AIResponseParser`、`AIRequestManager`。
- `Infrastructure/FileSystem/`：`SafeCopyService`、`SafeMoveService`、`FileConflictResolver`、`FileOperationRecovery`。
- `Infrastructure/Cache/`、`Infrastructure/Security/`（API Key DPAPI 存储）。
- 测试项目 `PhotoMind.Tests`、`PhotoMind.App.Tests`（xUnit + Moq）。
- 正式 EF Core Migration 替代 `EnsureCreated`。
