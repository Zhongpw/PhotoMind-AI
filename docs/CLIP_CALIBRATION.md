# CLIP 置信度校准与省 Token 评估报告

> 任务 04 交付文档。记录本地 CLIP 零样本分类器的置信度诊断、Prompt Ensemble 优化、
> 置信度校准方法与独立验证集评估结果。全部结论基于真实照片与本地推理，未伪造任何数据。

## 1. 问题诊断

任务 03 交付时实测：验证集 14 张照片 top-1 准确率 71.4%（10/14），但最高 softmax 置信度仅
0.3017，0.8 阈值下路由到 CLIP 的数量为 0，DeepSeek 请求减少 0%。核心问题不是 CLIP 分不对，
而是置信度被系统性低估，导致"省 Token 模式"从未实际触发。

逐项诊断 9 个环节后，结论如下：

| 环节 | 结论 |
|------|------|
| 图像特征生成（预处理） | ✅ 正确。ImageSharp `ResizeMode.Crop` = 最短边填充 + 中心裁剪，等价 HF `center_crop`+`shortest_edge`；Magick.NET `FillArea`+`Crop(Gravity.Center)` 一致；RGB `(p/255 - mean)/std` → NCHW `[1,3,224,224]`，与 `preprocessor_config.json` 完全一致 |
| 文本特征生成（分词器） | ✅ 正确。byte-level BPE，小写、空格→'Ġ'(U+0120)、`[49406]`+ids+`[49407]`、pad 到 77，忠实于 HF CLIPTokenizer |
| 特征归一化 | ✅ 正确。图像与文本嵌入均做 L2 归一化 |
| 余弦相似度 | ✅ 正确。归一化向量点积 = 余弦相似度 |
| Softmax 温度 | ⚠️ **主因**。`Softmax(logits, temp)` 做 `logits/temp`，logits=原始余弦相似度（图片-文本约 0.15–0.35），`temp=0.07` 只放大 14.3×，softmax 后仍近均匀（top 约 0.19–0.30）。CLIP 原生训练用学习到的 logit_scale≈100（即温度≈0.01），远强于 0.07 |
| 两级匹配 | ✅ 正确。6 顶级 softmax→top1→该顶级子类 softmax |
| Prompt | ⚠️ **次因**。26 类原为单条英文 Alias，无 ensemble；"everyday life"/"miscellaneous"/"other animals" 等兜底类语义过宽，压平顶级 softmax |
| 文本缓存 | ✅ 正确（已升级：缓存 key 含 prompt 签名，分类体系变化自然失效） |
| 置信度 vs 准确率 | ❌ 失配。softmax 概率 ≠ 真实分类概率，顶级 softmax 值被 6 个相近类别摊薄 |

## 2. Prompt Ensemble 优化

把 26 个类别的单条 Alias 升级为每条 3 个自然、具体的英文名词短语（共 78 条），CLIP 对每个
prompt 单独编码后取球面均值（L2 归一化后求平均再归一化），作为该类别的文本嵌入。分类 ID 与
数据库父子关系完全保留；`PromptsJson` 存入 `Categories` 表新列，通过 EF Core 迁移
`AddCategoryPrompts` 落地。

同 14 张验证集的 before/after（单 prompt → ensemble）：

| 指标 | 单 prompt（任务 03） | ensemble（任务 04） |
|------|---------------------|---------------------|
| top-1 准确率 | 10/14 = 71.4% | 11/14 = 78.6% |

其中 `bird`（鸟）从误判"自然"修正为正确的"动物"。n=14 过小，提升量在噪声范围内，但方向为正。

## 3. 置信度校准

实现 `ClipCalibration.Calibrate(margin, slope, intercept) = sigmoid(slope·margin + intercept)`，
`margin = top1Sim − top2Sim`（原始余弦差值）；`slope ≤ 0` 时返回 NaN 表示禁用（回退原始
softmax）。同时评估了基于 `top1Sim` 的 Platt/温度缩放形式。系数须在独立校准集上拟合。

独立验证集（78 张真实 Commons 照片，分层随机切分：47 张拟合 / 31 张留出）结果：

```
top-1 accuracy (top-level) : 57/78 = 73.1%
top-1 accuracy (sub-level)  : 41/59 = 69.5%
raw softmax conf  max/mean  : 0.2723 / 0.2143

[margin]   fitted: sigmoid(1.39*margin + 1.17)
[top1sim]  fitted: sigmoid(1.18*top1sim + 0.92)
```

两类校准在 0.8 阈值下的覆盖率均为 **0%**（最大校准置信度约 0.77 < 0.8），误采信 0/0。

各类别 top-1 准确率（顶级）：

| 类别 | 准确率 | 类别 | 准确率 |
|------|--------|------|--------|
| 动物 | 16/18 = 88.9% | 建筑 | 12/13 = 92.3% |
| 美食 | 16/17 = 94.1% | 自然 | 6/7 = 85.7% |
| 生活 | 7/18 = 38.9% | 其他 | 0/5 = 0.0% |

## 4. 诚实结论

1. **CLIP 有真实信号**：73.1% 顶级准确率远高于随机（6 类均匀基线 16.7%、多数类基线约 23%）。
2. **置信度仍无法达到 0.8**：无论温度缩放还是单特征 logistic 校准，最大校准置信度约 0.77。
   根因是 6 个宽泛顶级类别的图像-文本余弦相似度都挤在 0.18–0.27 的窄带里，margin（top1−top2）
   最大仅约 0.035，特征本身几乎不携带超出基准率（73%）的判别信息。
3. **0.8 阈值下 DeepSeek 请求减少比例 = 0.0%**。保留 0.8 为默认值，未擅自降低阈值。
4. **校准数据不足**：78 张（远不足 500 目标）、类别分布不均（动物 18/建筑 13/美食 17/生活 18/
   自然 7/其他 5，缺 the ocean/mountains/flowers/the sky/travel/a cat/a city/Western food 等 8 类），
   拟合系数（margin slope≈1.39、intercept≈1.17）**不具备泛化性，默认保持禁用**（slope=0），
   不作为运行时默认。
5. **无 GPU 实测**：本机无 NVIDIA GPU（仅虚拟显示设备、无 nvidia-smi/CUDA），CUDA 路径仅验证
   了"无 GPU → 自动回退 CPU"，未实测 GPU 推理。

**总体判断**：CLIP 零样本对当前 6 个宽泛顶级类别的区分信号不足以支撑 0.8 的可靠采信阈值。
这是分类任务本身的难度（顶级类别边界模糊、兜底类干扰）与 CLIP 零样本置信度天然未校准共同
导致，不是实现 bug。若未来要提升省 Token 比例，可选方向（未实施，供决策）：(a) 换更细的
分类体系或更强的 CLIP 变体；(b) 在更大、平衡、人工标注的数据集上重新拟合校准并接受更低阈值；
(c) 用 CLIP 只做粗筛（如排除明显不可能的类别）而非直接采信。

## 5. 修改文件清单

- `src/PhotoMind.Core/Models/Category.cs` — 新增 `PromptsJson`（Prompt Ensemble）。
- `src/PhotoMind.Core/Models/ClipCalibration.cs` — 新增（sigmoid + Calibrate）。
- `src/PhotoMind.Core/Constants/AiConstants.cs` — 新增校准默认值常量。
- `src/PhotoMind.Core/DTOs/AiConfig.cs` — 新增 `ClipCalibrationSlope`/`ClipCalibrationIntercept`。
- `src/PhotoMind.Core/DTOs/ClipMatchResult.cs` — 新增 `Top1Similarity`/`Top2Similarity`/`Margin`。
- `src/PhotoMind.Infrastructure/AI/ClipModelService.cs` — Prompt Ensemble（多 prompt 球面均值）+ margin 计算 + 缓存 key 含 prompt 签名。
- `src/PhotoMind.Infrastructure/Services/ClassificationService.cs` — 校准置信度门控（slope>0 时用校准值，否则回退 raw softmax）。
- `src/PhotoMind.Infrastructure/Database/CategorySeed.cs` — 26 类每类 3 条自然英文 prompt。
- `src/PhotoMind.Infrastructure/Migrations/20261124141607_AddCategoryPrompts.cs`（+ Designer + snapshot）— `Categories` 加 `PromptsJson`。
- `src/PhotoMind.Tests/Models/ClipCalibrationTests.cs` — 新增（6 例）。
- `src/PhotoMind.Tests/Services/ClassificationServiceTests.cs` — 新增 2 例校准门控测试。
- `.tools/clip-eval/Program.cs` — 独立评估工具重写（CSV 导出、分层切分、双特征 logistic 拟合、覆盖率/误采信率统计）。
- `.tools/clip-eval/download-validation2.mjs` — 新增（验证集下载脚本）。

## 6. 编译与测试结果

- 全量 sln build（Debug）：**EXIT=0**，0 警告 0 错误。
- PhotoMind.Tests：**104/104 PASS**（原 96 + ClipCalibration 6 + 校准门控 2）。
- PhotoMind.App.Tests：**30/30 PASS**。
- 合计 **134/134 PASS**。
