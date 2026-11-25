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
| Magick.NET-Q8-AnyCPU | 13.10.0 | TIFF/HEIC/RAW 解码（ImageMagick 封装） | Apache-2.0 | 含 libheif（解码）/libraw 原生库 |
| Microsoft.ML.OnnxRuntime.Gpu.Windows | 1.32.0 | 本地 CLIP 图像识别推理（CPU/CUDA） | MIT | 依赖 Microsoft.ML.OnnxRuntime.Managed |
| CLIP 模型（Xenova/clip-vit-base-patch32） | ViT-B/32 | 本地零样本图像分类 | MIT（OpenAI CLIP） | ONNX 权重，见下方说明 |

## CLIP 模型文件与 SHA-256

本地 CLIP 识别使用 Hugging Face 上的 **Xenova/clip-vit-base-patch32**（OpenAI `openai/clip-vit-base-patch32` 的 ONNX 转换），模型权重许可证为 **MIT**（OpenAI CLIP）。

- 来源：https://huggingface.co/Xenova/clip-vit-base-patch32 （经 hf-mirror.com 镜像下载）
- 存放位置（v1.1.0 起）：优先安装包内置 `models/clip/`（随程序目录部署）；用户数据目录 `<数据目录>/models/clip/` 作为回退（**模型文件不进 Git**，构建/打包前需先下载到 `.models/clip/`）

| 文件 | 大小（字节） | SHA-256 |
|---|---|---|
| vision_model.onnx | 351,685,709 | `FD6E1402A588279D1723C7534D4BCBA5BC0B14B47DFAB0E46F8C47B8270D7D40` |
| text_model.onnx | 254,058,553 | `3F6571F5BAD13A97C469C1622E1CFC4D9AEF78B79FDBFCFF804CA357BFADA8CC` |
| vocab.json | 862,328 | `5047B556CE86CCAF6AA22B3FFCCFC52D391EA4ACCDAB9C2F2407DA5B742D4363` |
| merges.txt | 524,619 | `9FD691F7C8039210E0FCED15865466C65820D09B63988B0174BFE25DE299051A` |
| config.json | 4,524 | `493EF57FF783E42D1530C91B53469B7FDF8DB8A9C1408E86998FCB7899A4F495` |
| preprocessor_config.json | 520 | `6F638FB9401A6D6296FEFF533EE7EFE657B787C49F954F82F5906B36EF2A1B1F` |

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
