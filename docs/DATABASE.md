# PhotoMind AI — 数据库设计

## 概述

- 数据库引擎：**SQLite**（单文件，零配置，适合本地桌面应用）。
- 数据访问：**EF Core 8**（`Microsoft.EntityFrameworkCore.Sqlite` 8.0.34）。
- 连接字符串：`Data Source={DatabasePath}`，`DatabasePath` 由 `AppPaths` 提供。
- 初始化：应用启动时 `DatabaseInitializer.InitializeAsync` 调用 `EnsureCreatedAsync` 建库建表，随后执行分类种子（`CategorySeed`）。**当前为 Phase 1–6 的 EnsureCreated 方案，迁移（Migration）留作后续迭代。**
- 数据库文件：`photomin.db`（默认 `%LOCALAPPDATA%\PhotoMind AI\photomin.db`，可用环境变量 `PHOTOMIND_DATA_DIR` 覆盖数据目录）。
- 事务：EF Core 默认每 `SaveChanges` 一个事务；扫描/分类/整理均分批提交（`BatchSize`）。

## 表结构

`AppDbContext` 注册 9 个 DbSet：`Images`、`Categories`、`ClassificationResults`、`ScanTasks`、`ClassificationTasks`、`OrganizeTasks`、`OrganizeLogs`、`UserRules`、`ApiUsages`。

### Images（图片）

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | long（主键） | 自增 |
| FileName | string | 文件名 |
| OriginalPath | string（索引） | 原图绝对路径 |
| FileSize | long | 字节数 |
| FileHash | string（索引） | SHA-256 |
| Width / Height | int | 像素尺寸 |
| Format | ImageFormat | Jpeg/Png/WebP/Bmp/Gif/Tiff/Heic/Raw/Unknown |
| CreatedAt / ModifiedAt | DateTime | 文件时间 |
| ExifDate | DateTime? | EXIF 拍摄时间（仅本地，不上传） |
| ScanStatus | ImageScanStatus | Pending/Scanned/Failed/Skipped |
| CreatedTime | DateTime | 入库时间 |
| ScanTaskId | long?（索引） | 所属扫描任务 |
| ThumbnailPath | string? | 缩略图缓存路径 |

### Categories（分类，多级）

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | long（主键） | |
| Name | string | 分类名 |
| ParentId | long?（索引） | 父分类（null=顶级） |
| Description | string | 描述 |
| IsSystem | bool | 是否系统内置（种子分类不可删） |
| SortOrder | int | 排序 |
| CreatedAt | DateTime | |

默认种子（IsSystem=true）：自然/生活/动物/建筑/美食/其他 六个顶级分类及子分类。

### ClassificationResults（分类结果）

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | long（主键） | |
| ImageId | long（索引） | 图片 |
| CategoryId | long?（索引） | 一级分类 |
| SubCategoryId | long? | 二级分类 |
| Description | string（MaxLength 4096） | 描述 |
| Reason | string（MaxLength 4096） | 理由 |
| Confidence | double | 置信度 0–1 |
| TagsJson | string（MaxLength 4096） | 标签 JSON |
| NeedsReview | bool | 是否需人工审核 |
| ModelName | string（128） | 模型名 |
| PromptVersion | string（64） | 提示词版本 |
| CreatedAt / UpdatedAt | DateTime | |

### ScanTasks（扫描任务）

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | long（主键） | |
| SourcePath | string | 源路径（JSON 数组字符串） |
| TotalCount / ScannedCount / FailedCount | int | 计数 |
| Status | TaskState | Pending/Running/Paused/Completed/Failed/Cancelled |
| StartedAt | DateTime（索引） | |
| FinishedAt | DateTime? | |

### ClassificationTasks（分类任务）

字段：Id、TotalCount、CompletedCount、FailedCount、PendingCount、Status(TaskState)、StartedAt、FinishedAt。

### OrganizeTasks（整理任务）

字段：Id、SourcePath、DestinationPath、OperationType(OrganizeOperationType)、TotalCount、CompletedCount、FailedCount、Status(TaskState)、StartedAt、FinishedAt。

### OrganizeLogs（整理日志，用于撤销）

字段：Id、TaskId（索引）、ImageId（索引）、SourcePath、DestinationPath、OperationType、FileHashBefore、FileHashAfter、Status、ErrorMessage、CreatedAt。

### UserRules（用户规则，预留）

用户自定义分类/整理规则，当前阶段预留。

### ApiUsage（API 用量）

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | long（主键） | |
| TaskId | long?（索引） | 关联任务 |
| ModelName | string（128） | 模型 |
| InputTokens / OutputTokens / TotalTokens | int | token 数 |
| EstimatedCost | decimal | 估算费用 |
| IsEstimated | bool | token 是否为估算 |
| RequestStatus | string（128） | 请求状态 |
| CreatedAt | DateTime（索引） | |

## 索引

- Images：OriginalPath、FileHash、ScanTaskId。
- Categories：ParentId。
- ClassificationResults：ImageId、CategoryId。
- ScanTasks：StartedAt。
- OrganizeLogs：TaskId、ImageId。
- ApiUsage：TaskId、CreatedAt。

## 关键设计决策

1. **短生命周期 DbContext**：仓储使用 `IDbContextFactory<AppDbContext>`，每次操作新建 context，避免跨线程共享。
2. **分类结果与整理分离**：分类结果只存建议，物理文件操作在 OrganizeLogs 记录前/后哈希，保证可撤销。
3. **重复检测**：`Images.FileHash`（SHA-256）分组，用于历史页展示重复图片。
4. **SQLite 类型注意**：`decimal`（EstimatedCost）无法直接 `Sum` 聚合，用量统计用 `(double)x.EstimatedCost` 求和后再转回 `decimal`。
