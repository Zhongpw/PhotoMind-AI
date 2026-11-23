# 更新日志 (Changelog)

本文件记录 PhotoMind AI 各开发阶段的变更。

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
