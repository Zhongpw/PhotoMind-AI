# 更新日志 (Changelog)

本文件记录 PhotoMind AI 各开发阶段的变更。

## [0.12.0] — 修复：CLIP 模型随安装包部署（安装包版本 1.1.0）

**问题**

- 此前 CLIP 模型（`.models/clip/`，约 607 MB，不进 Git）未被打包进发布产物 / 安装包：`PhotoMind.App.csproj` 缺少把模型复制到输出目录的规则，且 `ClipModelService` 只从用户数据目录加载模型，全新安装后报「CLIP 模型文件缺失」。

**修复**

- `src/PhotoMind.App/PhotoMind.App.csproj`：新增 `<None Include="..\..\.models\clip\*">`（`CopyToOutputDirectory` / `CopyToPublishDirectory` = `PreserveNewest`），模型随 Debug/Release/publish 输出到 `models/clip/`，Inno Setup 打包的 publish 目录因此自带模型。
- `src/PhotoMind.Infrastructure/AI/ClipModelService.cs`：新增 `ResolveModelDirectory()`，优先搜索程序目录 `AppContext.BaseDirectory/models/clip`，回退用户数据目录，错误提示列出两个搜索路径。
- `installer/PhotoMindAI.iss`：`AppVersion` 1.0.0 → 1.1.0。

**注意**

- 模型文件不进 Git。全新 `git clone` 后必须先 `node .tools/download-clip-model.mjs` 下载模型到 `.models/clip/`，再 `scripts/publish.ps1` + ISCC 打包，安装包才会包含模型。

## [0.11.0] — 增量任务 05：CLIP 本地粗筛 + DeepSeek 协同分类

**新增功能**

- CLIP 候选类别筛选（`ClipModelService.GetTopKCandidatesAsync`）：对图片与**全部 26 个类别（顶级+子类）**的原始余弦相似度扁平排序取 Top-K（默认 5，可配 3/5/8/全部），候选全部来自数据库、不硬编码；`ClipCandidate` 记录类别/父分支/相似度。
- DeepSeek 协同提示词（`PromptBuilder`）：候选非空时在系统提示词前置"本地图像模型已初步筛选的候选分类（按相似度降序，仅供参考，不要盲从）"并保留完整顶级分类体系作兜底——**DeepSeek 仍独立观察原始图片、可越出候选、绝不因 CLIP 排名错误而被限制在错误类别**。
- 双开关分离：设置页「本地 CLIP 识别」卡片新增「CLIP 候选类别筛选」开关 + Top-K 选择（3/5/8/全部），与「省 Token 直接采信（0.8 阈值）」相互独立；`AiConfig.ClipCandidateFiltering`/`ClipTopK` + `AiConstants.DefaultClipTopK=5` + `Sanitized` 收敛（clamp 0..100）。
- 候选追踪统计：`ClassificationResult.CandidateCount`/`CandidateHit`（null=未启用）+ `AddCandidateTracking` 迁移 + `GetCandidateStatsAsync` 聚合（筛选张数/平均候选数/命中数/候选外数）；历史页新增候选统计行。图片嵌入一次编码同时供直接采信与候选筛选复用。

**验证（诚实结论）**

- 全量离线编译通过（EXIT=0，5 项目，0 警告 0 错误）；单元测试 **142/142** 通过（PhotoMind.Tests 110/110 + PhotoMind.App.Tests 32/32）。
- 独立验证集 78 张真实照片（本地 CLIP，不调用 DeepSeek）：**候选命中率 K=3 时 70/78=89.7%、K=5 时 73/78=93.6%、K=8 时 77/78=98.7%、全部时 100%**——正确顶级类别几乎不会被候选筛除（召回高，兜底可靠）。
- **输入 Token 诚实结论**：因保留完整顶级分类体系作为兜底（满足"DeepSeek 可越出候选"的保护要求），候选提示词相比无候选基线（576 字符≈144 token）**反而更长**（K=3 约 +123 字符、K=5 +143、K=8 +175），输入 Token 并未下降。候选筛选的价值在于子类别精确引导与高召回兜底，**不构成输入 Token 节省**；最终分类准确率/输出 Token/成本/耗时需真实 DeepSeek 调用方可对比，本环境不消耗 API 额度、无法实测，如实说明。

## [0.10.0] — 增量任务 04：CLIP 置信度校准与省 Token 优化

**新增功能**

- Prompt Ensemble：26 个类别从单条英文别名升级为每条 3 个自然、具体的英文名词短语（共 78 条），CLIP 对每个 prompt 单独编码后取球面均值（L2 归一化平均）作为类别嵌入；`Category.PromptsJson` 存 JSON 数组，`AddCategoryPrompts` 迁移落地，分类 ID 与父子关系完全保留。
- 置信度校准：新增 `ClipCalibration`（`calibrated = sigmoid(slope·margin + intercept)`，`margin = top1Sim − top2Sim` 原始余弦差值），`ClipMatchResult` 新增 `Top1Similarity`/`Top2Similarity`/`Margin`，`ClassificationService` 在 `ClipCalibrationSlope > 0` 时用校准置信度门控，否则回退原始 softmax（默认禁用）。
- 独立评估工具（`.tools/clip-eval/`）：导入文件夹树（文件夹名=人工标注类别）、批量本地 CLIP 推理、导出 CSV、分层随机切分（拟合/留出）、双特征 logistic 拟合（margin / top1sim）、统计 top-1 准确率/各类别准确率/覆盖率/误采信率/API 减少比例；不调用 DeepSeek、不改正式分类库。
- 验证集下载脚本（`.tools/clip-eval/download-validation2.mjs`）：Wikimedia Commons 真实照片，按英文别名分类落盘。

**验证（诚实结论）**

- 全量离线编译通过（EXIT=0，5 项目，0 警告 0 错误）。
- 单元测试 134/134 通过（PhotoMind.Tests 104/104 + PhotoMind.App.Tests 30/30）。
- 独立验证集 78 张真实照片：top-1 准确率 **57/78 = 73.1%**（随机基线 16.7%）；子类 41/59 = 69.5%。Prompt Ensemble 使同 14 张验证集从 71.4%（10/14）提升到 78.6%（11/14，其中"鸟"从误判"自然"修正为"动物"）。
- 校准拟合：margin 法 `sigmoid(1.39·margin+1.17)`、top1sim 法 `sigmoid(1.18·top1sim+0.92)`；**两类校准在 0.8 阈值下覆盖率均为 0%**（最大校准置信度约 0.77 < 0.8），DeepSeek 减少比例 0.0%，误采信 0/0。
- 结论：CLIP 有真实信号（73.1% 远超随机），但 6 个宽泛顶级类别的图像-文本余弦相似度挤在 0.18–0.27 窄带、margin 最大仅约 0.035，特征几乎不携带超出基准率的判别信息，导致 0.8 阈值不可达。**保留 0.8 默认值、未降阈值；校准数据不足（78 张远不足 500、分布不均），拟合系数不具备泛化性，运行时默认禁用（slope=0）**。无 GPU 实测（本机无 NVIDIA GPU，仅验证 CUDA 自动回退）。详见 `docs/CLIP_CALIBRATION.md`。

## [0.9.0] — 增量任务 03：本地 CLIP 识别 + 省 Token + GPU 加速

**新增功能**

- 本地 CLIP 图像识别（`ClipModelService`，ONNX Runtime 1.32.0 + `Xenova/clip-vit-base-patch32`）：CPU 推理，零外部 API 调用。图像编码 224×224 → 512 维嵌入、文本编码 77 token → 512 维嵌入、两级分层匹配（先顶级再子类），CLIP 文本嵌入带 `clip-text-cache.json` 持久化缓存。模型文件独立存放于 `<数据目录>/models/clip/`，不进 Git。
- 省 Token 分流（`ClassificationService`）：开启「省 Token 模式」后，每张图片先用本地 CLIP 匹配，置信度 ≥ 阈值（默认 0.8，可配置）直接采信并写 `ClassificationSource.Clip`、**不调用 DeepSeek、不写 ApiUsage**；低置信度或 CLIP 失败自动回退 DeepSeek（`Source.DeepSeek`），分类结果与审核界面无缝兼容。
- GPU 加速开关（NVIDIA CUDA）：`SessionOptions.AppendExecutionProvider_CUDA(0)`，无 GPU/缺 cuDNN 时自动降级回 CPU（`Degraded=true` + 中文回退原因），单一 GPU 版 ONNX Runtime 二进制同时支持 CPU/CUDA，做成真正的运行时可切换开关（非结构性空操作）。
- 设置页 UI（`SettingsView`）：新增「本地 CLIP 识别（省 Token）」卡片，两个独立开关（省 Token 模式 / GPU 加速）+「检测设备」按钮 + 实时设备状态显示（CPU / NVIDIA CUDA / 回退原因 / 加载失败原因）。
- 分类类别英文别名（`Category.Alias` + `AddClipFields` 迁移 + 种子 26 类全部加英文别名），CLIP 用英文 prompt（`a photo of {alias}`）匹配，识别率远优于中文。
- 模型来源/版本/许可证/SHA-256 记录（见 `docs/THIRD_PARTY_NOTICES.md`）。

**验证**

- 全量离线编译通过（EXIT=0，5 项目，仅良性 NU1603 版本解析警告）。
- 单元测试 126/126 通过（PhotoMind.Tests 96/96 + PhotoMind.App.Tests 30/30）。
- CLIP CPU 冒烟 18/18（模型加载、图像/文本编码 512 维、L2 归一化、分层匹配、文本缓存、GPU 回退降级）。
- 真实验证集（Wikimedia Commons 14 张照片，5 大类）：**top-1 准确率 10/14 = 71.4%**；0.8 阈值下路由 CLIP 0/14（DeepSeek 减少比例 0.0%）、0.8 阈值误采信 0/0。详见交付报告。

## [0.8.0] — 增量改进：API 状态显示 + 密钥健壮性 + 扫描列表移除

**新增功能**

- 扫描列表移除（右键「从任务列表移除」，支持单张/多选批量）：复用 `ImageScanStatus.Skipped` 作为"从当前任务队列移除"标记（不新增表/列/迁移，零重构）。`MarkImagesSkippedAsync` 只把状态从 Scanned→Skipped，绝不删除全局 Image 记录、分类结果、缓存或原始文件；`GetImagesByTaskAsync` 过滤 Skipped 使移除后 UI 立即刷新；分类/整理/重复检测三处均已按 Scanned 过滤，故移除项自动被排除。右键菜单 + 扩展多选 + MessageBox 确认 + 立即刷新。
- API 状态显示（主界面右上角）：区分未配置/已配置/已连接/连接失败/密钥异常五态，`ApiStatusDetector` + `ApiStatusChangedMessage` 广播（保存/删除/测试后实时刷新）。
- 密钥存储健壮性：`SecretStore.GetSecretAsync` 对非法 base64 返回 null（视为"无法解密"），不再抛未处理异常（避免密钥异常被误判为"未配置"）。

**验证**

- 全量离线编译通过（EXIT=0，5 项目，仅良性 NU1603 版本解析警告）。
- 单元测试 120/120 通过（PhotoMind.Tests 92/92 + PhotoMind.App.Tests 28/28）。
- 移除功能冒烟验证通过（12/12：单移、批移、任务队列刷新、原图保留、分类结果不变、重扫仍找到、未分类队列排除、任务状态不变、跨任务无影响）。

## [0.7.0] — Phase 7：增强（单测 / 迁移 / 扩展格式 / 正式安装包）

**新增功能**

- 单元测试（`PhotoMind.Tests` 89 例 + `PhotoMind.App.Tests` 11 例 = 100 例）：覆盖魔数格式检测（含 RAW）、路径安全、文件冲突递增、SHA-256、AI 响应解析、DeepSeek 异常重试分类、AI 配置钳制、暂停令牌、DeepSeek 契约序列化/反序列化、设置页 ViewModel、分类页 ViewModel。测试项目手动接入解决方案（`dotnet sln add` 会触发 MSBuild workload 解析问题）。
- EF Core 迁移（`InitialCreate` 迁移 + `AppDbContextModelSnapshot` + `DesignTimeDbContextFactory`）：启动时 `MigrateAsync` 自动建库/升级（替代 `EnsureCreatedAsync`），数据库结构纳入版本管理。
- TIFF / HEIC / RAW 完整本地解码（`Magick.NET-Q8-AnyCPU` 13.10.0 + `MagickImageDecoder`）：格式检测新增 RAW（RAF/ORF/RW2/CR2/NEF/ARW/DNG），元数据读取、缩略图与 AI 预览统一本地转 JPEG（HEIC 仅解码，符合"只读原图"原则）。
- 正式安装包（`installer/PhotoMindAI.iss`，Inno Setup 6.7.4，`PrivilegesRequired=lowest` 免管理员）：`ISCC` 编译出 `PhotoMindAI-Setup-1.0.0.exe`（51.6 MB，自包含、含中文语言、开始菜单/桌面快捷方式、卸载程序）。
- MSBuild workload 解析根因修复：本机 SDK 缺 `WorkloadAutoImportPropsLocator` 的 `AutoImport.props/targets` → 补建 + `DisableWorkloadResolver.sentinel`，此后所有 `dotnet`/`msbuild`/`dotnet-ef` 命令不再需要 `-p:MSBuildEnableWorkloadResolver=false`。

**验证**

- 全量离线编译通过（Release，5 项目，EXIT=0，仅良性 NU1603 版本解析警告）。
- 单元测试 100/100 通过（PhotoMind.Tests 89/89 + PhotoMind.App.Tests 11/11）。
- TIFF/HEIC/RAW 冒烟验证通过（18/18：检测、IsSupported、RAW 头、元数据、缩略图、AI 预览）。
- 安装包安装测试通过（测试版 `PhotoMindAI-Setup-Test.exe`：INSTALL_EXIT=0、493 文件、安装后应用启动正常、SQLite 迁移 + 分类种子自动写入）。

## [0.6.0] — Phase 6：最终发布

**新增功能**

- 自包含 x64 发布（`scripts/publish.ps1` + 运行时包离线下载），不依赖目标机 .NET 运行时。
- 安装/卸载脚本（`scripts/install.ps1` / `scripts/uninstall.ps1`）：安装到 `%LOCALAPPDATA%\Programs\PhotoMind AI` + 开始菜单快捷方式；卸载默认保留用户数据（须输 YES 才删，且永远不动原图）。
- 第三方许可声明（`docs/THIRD_PARTY_NOTICES.md`）：MIT/Apache-2.0；ImageSharp = Six Labors Split License v1.0（年收入 ≥100 万美元组织需商业许可，可换 SkiaSharp/Magick.NET）。
- 文档补齐：`docs/DATABASE.md`（9 张表/索引/决策）、`docs/DEEPSEEK_API.md`（接口/Key 存储/预处理/重试/用量）、`docs/USER_GUIDE.md`（安装/首次使用/整理安全/隐私）、`docs/TEST_PLAN.md`（测试层次/工具/结果/数据安全断言）。

**验证**

- 全量离线编译通过（EXIT=0，仅良性 NU1603 版本解析警告）。
- Release 自包含发布通过（270 文件，154.7 MB，含 WPF + SQLite 原生库）。
- 发布版 exe 冒烟验证通过（无需 DOTNET_ROOT，可独立运行、SQLite 建库正常）。
- 5 个 PowerShell 脚本通过 `[Parser]::ParseFile` 语法校验（0 错误）。

## [0.5.0] — Phase 5：任务恢复与优化

**新增功能**

- 任务中断恢复（`TaskRecoveryService`）：启动时把 Pending/Running/Paused 的扫描/分类/整理任务标记为 Failed，防止异常退出后状态悬挂。
- 重复图片检测（`IScanRepository.GetDuplicateGroupsAsync`）：按 SHA-256 分组，仅提示、不删除。
- API 用量统计（`IClassificationRepository.GetUsageSummaryAsync`）：累计请求数、输入/输出/总 Token、估算费用与估算请求数。
- 历史任务页（`HistoryViewModel`/`HistoryView`）：三类任务记录（扫描/分类/整理）+ 重复图片组 + API 用量摘要。
- 缩略图解码并发限制（`ThumbnailService`：`SemaphoreSlim` 按 CPU 核数限制同时解码数量）。

**验证**

- 全量离线编译通过（EXIT=0，仅良性 NU1603 版本解析警告）。
- 应用启动验证通过（WPF 可运行、SQLite 可连接、启动时任务恢复正常）。
- 任务恢复/重复/用量冒烟验证通过（10/10）。
- 10,000 张图片性能测试通过：扫描 49.35s、峰值内存 100.5 MB、分页查询 14.3 ms、0 失败。

## [0.4.0] — Phase 4：照片整理

**新增功能**

- 整理预览（`OrganizeService.PreviewAsync`）：照片总数、分类数、预计文件夹数、预计磁盘占用、文件冲突数、错误数。
- 安全文件操作（`SafeFileOperations`/`PathSafety`/`FileConflictResolver`）：复制/移动前校验源文件、目标路径合法性（防路径穿越、防系统保护目录、防设备名），同名文件自动 `photo_1.jpg` 递增不覆盖，复制后大小 + SHA-256 校验，失败即删副本。
- 整理执行（`OrganizeService.ExecuteAsync`）：仅建目录 / 复制（默认）/ 移动三种操作，支持暂停、继续、取消，逐文件写整理日志（源/目标路径 + 前后哈希）。
- 移动安全：同盘直接重命名；跨盘先复制校验再删源，校验失败不删源。
- 整理撤销（`OrganizeService.UndoAsync`）：复制→按日志校验哈希后删除副本；移动→按日志移回原位置（目标已存在同名时拒绝覆盖）。
- 照片整理页（`OrganizeViewModel`/`OrganizeView`）：目标目录选择、操作类型、预览摘要、确认勾选、进度、撤销（二次确认）。

**验证**

- 全量离线编译通过（EXIT=0，仅良性 NU1603 版本解析警告）。
- 应用启动验证通过（WPF 可运行、SQLite 可连接）。
- 整理冒烟验证通过（31/31：预览计数、复制保原图、冲突递增、撤销副本、移动 + 撤销恢复、仅建目录，前后 SHA-256 一致）。

## [0.3.0] — Phase 3：分类体系与审核

**新增功能**

- 默认分类体系（`CategorySeed`：自然/生活/动物/建筑/美食/其他 6 个一级分类及子分类，首次运行自动种子）。
- 分类管理页（`CategoryManagementViewModel`/`CategoryManagementView`）：树形展示、添加一级/子分类、重命名、删除（保护系统分类与有子分类的分类）。
- 分类审核页（`ReviewViewModel`/`ReviewView`）：图片墙虚拟化列表、按一级/二级分类/置信度/待审核/关键词筛选、单张与批量修改分类、标记待审核/已审核/未分类、查看 AI 描述与分类理由、原始路径展示。
- 仓储扩展（`ClassificationRepository`）：分类 CRUD、结果分页筛选查询（`ReviewFilter`）、结果更新/删除。

**验证**

- 全量离线编译通过（EXIT=0，仅良性 NU1603 版本解析警告）。
- 应用启动验证通过（WPF 可运行、SQLite 可连接、分类种子正常）。
- 分类/审核冒烟验证通过（20/20：种子 6 分类、筛选、修改分类落库、分类 CRUD）。

## [0.2.0] — Phase 2：DeepSeek API 接入

**新增功能**

- AI 配置与视觉识别契约（`DeepSeekContracts`、`AiConfig`、`AiRecognitionResult`）。
- API Key 安全存储（`SecretStore`，Windows DPAPI 加密，不落明文、不写日志）。
- AI 配置持久化（`AiConfigStore`，JSON 存本地，只存非敏感参数）。
- 图片预处理（`ImageNormalizer`：EXIF 方向校正 + 按配置缩放 + JPEG 压缩，输出临时预览，绝不修改原图）。
- DeepSeek 客户端（`DeepSeekClient`，OpenAI 兼容 `/chat/completions`，错误分类映射）。
- 提示词构建（`PromptBuilder`，按 Auto/Fixed/Hybrid 三种模式生成系统提示词）。
- 响应解析（`AIResponseParser`：JSON 提取、字段校验、置信度钳制、分类名路径字符过滤）。
- 请求重试（`AIRequestManager`：指数退避 + 随机抖动）。
- 分类持久化（`ClassificationRepository`）与分类编排（`ClassificationService`：并发、暂停、取消、分类解析、API 用量记录）。
- 设置页（API Key 管理 + 测试连接 + 模型/并发/重试/预览配置）。
- AI 智能分类页（选择图片或全部未分类、分类模式、运行/暂停/恢复/取消、实时进度、结果列表）。

**验证**

- 全量离线编译通过（EXIT=0，仅良性 NU1603 版本解析警告）。
- 应用启动验证通过（WPF 可运行、SQLite 可连接、DPAPI 配置路径正常）。
- Mock 验证通过（17/17：JSON 解析、提示词构建、分类落库、分类名解析）。

## [0.1.0] — Phase 0 + Phase 1：项目初始化 + 图片扫描

**新增功能**

- 项目初始化（.NET 8 解决方案、Core/Infrastructure/App 分层架构、离线 NuGet 源）。
- 基础 WPF 程序（主界面、左侧导航栏、首页、Fluent 简洁风格）。
- 图片扫描（多目录选择、递归扫描、文件内容魔数识别格式、缩略图缓存、网格虚拟化、扫描记录落库）。
- SQLite + EF Core（自动初始化、WAL 模式）。
- 目录扫描防护（跳过系统保护目录、无权限目录、符号链接、自身数据目录）。

**验证**

- 全量离线编译通过。
- WPF 可运行、SQLite 可连接验证通过。
- 扫描验证通过（6 张有效图正确识别并生成缩略图、损坏图标记失败、扫描前后原图 SHA-256 一致）。
