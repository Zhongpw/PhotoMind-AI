# PhotoMind AI — 测试计划

## 一、测试层次

| 层次 | 说明 | 工具 |
|---|---|---|
| 单元测试 | 类型判断/路径过滤/hash/JSON 解析/分类校验/冲突/路径合法性/撤销/重试 | xUnit（PhotoMind.Tests，预留） |
| 集成测试 | SQLite 初始化/扫描/缩略图/分类写入/整理/撤销/恢复 | 各 smoke 控制台 harness（ProjectReference 到 Core+Infrastructure） |
| 数据安全测试 | 原始 hash 不变/仅分类不改路径/移动失败源还在/不覆盖/撤销不误删/可恢复 | 各 smoke 断言 |
| 性能测试 | 1k/5k/10k 张测试图 | perf10k harness |

> 说明：Phase 0–6 采用**离线编译**（NuGet 离线源），xUnit 测试项目尚未接入离线源，故当前用独立 console harness（`ProjectReference` 引用 Core+Infrastructure）做集成验证；Phase 后续可将 xUnit/Moq 接入离线源补齐单元测试。所有测试**只用 TestData 生成的测试图**，绝不对真实照片做破坏性测试。

## 二、测试工具（`.tools/`）

| 工具 | 验证内容 |
|---|---|
| `scan-smoke` | 扫描（魔数识别/递归/缩略图/原图 hash 不变） |
| `mock-smoke` | 分类全链路（假 IDeepSeekClient，parse→resolve→save→usage） |
| `api-smoke` | 真实 DeepSeek 连通 + 分类一张图（需 `PHOTOMIND_API_KEY`） |
| `phase3-smoke` | 分类种子/仓储 CRUD/结果筛选/更新 |
| `phase4-smoke` | 整理预览/复制/移动/撤销/冲突/原图 hash 不变 |
| `phase5-smoke` | 任务恢复/重复检测/用量统计/历史列表 |
| `perf10k` | 10k 张扫描性能 + 分页查询 |

## 三、已执行结果

| 阶段 | 结果 |
|---|---|
| Phase 1 扫描 | VERIFY PASS（6 张识别正确、原图 SHA-256 与 DB 一致） |
| Phase 2 Mock | 17/17 PASS |
| Phase 2 真实 API | 连通 OK + 分类 1 张（一级"插画"/二级"极简风景"/97%） |
| Phase 3 | 20/20 PASS |
| Phase 4 | 31/31 PASS |
| Phase 5 | 10/10 PASS |
| Phase 5 性能 | 10k 张：49.35s、峰值内存 100.5MB、分页查询 14.3ms、0 失败 |

## 四、数据安全断言（核心）

1. **原始图 hash 不变**：扫描/分类/整理（复制模式）前后原图 SHA-256 与 DB 记录一致。
2. **仅分类不改路径**：分类结果只写 ClassificationResults，不改 Images.OriginalPath。
3. **移动失败源还在**：跨盘移动先复制校验再删源，失败不删源。
4. **不覆盖**：同名冲突自动改名，绝不覆盖既有文件。
5. **撤销不误删**：复制撤销只删副本、移动撤销按日志移回且同名不覆盖。
6. **可恢复**：异常关闭后启动自动把中断任务标记 Failed。
7. **不递归扫描输出目录**：扫描跳过自身数据目录与保护目录。

## 五、测试图生成

- `TestData/sample/`：JPG/PNG/WebP/BMP/GIF/TIFF + 损坏文件 + 假魔数文件（Phase 1–4 用）。
- `TestData/perf10k/`：10,000 张 32×32 JPEG（`gen-perf-images.py` 生成）。
