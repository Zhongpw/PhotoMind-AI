# 第三方依赖与许可证声明 (Third-Party Notices)

PhotoMind AI 依赖以下第三方库。本文件记录其版本与许可证，用于合规审查与分发确认。

| 包名 | 版本 | 用途 | 许可证 | 说明 |
|---|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | MVVM（ObservableObject/RelayCommand） | MIT | 无额外义务 |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | 依赖注入 | MIT | 无额外义务 |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.34 | ORM + SQLite 数据库 | MIT | 含 Microsoft.Data.Sqlite、SQLitePCLRaw |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.13 | SQLite 原生库封装 | Apache-2.0 | SQLite 内核本身为公有领域 (Public Domain) |
| Serilog | 4.4.0 | 结构化日志 | Apache-2.0 | 无额外义务 |
| Serilog.Sinks.File | 6.0.0 | 日志文件输出 | Apache-2.0 | 无额外义务 |
| SixLabors.ImageSharp | 3.1.12 | 图片解码/缩放/缩略图 | Six Labors Split License v1.0 | 见下方说明 |

## SixLabors.ImageSharp 许可证特别说明

ImageSharp 3.x 采用 **Six Labors Split License v1.0**：

- 满足以下条件时可**免费使用**（按 Apache-2.0 条款）：
  - 组织年总收入（含关联方）**低于 100 万美元**；或
  - 用于开源项目（OSI 批准许可证）；或
  - 用于学习/个人/非商业目的。
- 若组织年收入 **≥ 100 万美元** 且用于商业闭源软件，需要购买**商业许可证**（详见 https://sixlabors.com/pricing ）。

**PhotoMind AI 的合规建议**：

1. 若您的使用场景符合上述免费条件，可继续使用 ImageSharp，无需额外授权。
2. 若为高收入商业使用，请购买 ImageSharp 商业许可证，或替换为等效的 MIT/Apache 库（如 SkiaSharp、Magick.NET），替换点集中在 `PhotoMind.Infrastructure/ImageProcessing/`（`ThumbnailService`、`ImageNormalizer`、`ImageMetadataReader` 三处），不影响其余架构。

## 许可证文件位置

各包随 NuGet 缓存自带完整许可证文本，可解压 `.nuget/packages/<包名>/<版本>/` 下的 `*.nupkg` 查看（`/package/LICENSE*` 或 `licenseUrl` 字段）。

本软件本身（PhotoMind AI）为独立开发的本地桌面软件，原始照片数据**不会**因使用上述库而获得额外授权。
