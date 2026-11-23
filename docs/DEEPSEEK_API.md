# PhotoMind AI — DeepSeek API 接入说明

## 接口概览

PhotoMind AI 通过 DeepSeek 的 **OpenAI 兼容接口** 调用视觉模型完成照片识别。

- Base URL：`https://api.deepseek.com`（默认，可在设置页修改）
- 端点：`POST {BaseUrl}/chat/completions`
- 认证：`Authorization: Bearer {API Key}`
- 默认模型：`deepseek-flash`（DeepSeek-V4.1-Flash，`input_modalities: ["text","image"]`，支持视觉）
- 图片传输：Base64 编码的 Data URL（`data:image/jpeg;base64,...`）

## 请求格式

`POST /chat/completions`，`Content-Type: application/json`：

```json
{
  "model": "deepseek-flash",
  "messages": [
    { "role": "system", "content": "<中文分类提示词>" },
    { "role": "user", "content": [
      { "type": "image_url", "image_url": { "url": "data:image/jpeg;base64,...", "detail": "low" } }
    ]}
  ]
}
```

- `detail`：`low`（低分辨率，默认）或 `original`（高分辨率，设置页可选）。
- system 提示词固定为任务书 6.3 的分类规则，要求**只返回 JSON、不带 Markdown**。根据分类模式（AI自动/Fixed/Hybrid）在提示词中附带已有分类清单。

## 响应格式

```json
{
  "choices": [ { "message": { "role": "assistant", "content": "<JSON 字符串>" }, "finish_reason": "stop" } ],
  "usage": { "prompt_tokens": 435, "completion_tokens": 443, "total_tokens": 878 }
}
```

分类结果 JSON（由 `AIResponseParser.Parse` 解析）：

```json
{
  "category": "自然",
  "subcategory": "海洋",
  "tags": ["海", "沙滩"],
  "description": "...",
  "reason": "...",
  "confidence": 0.93,
  "needs_review": false,
  "suggested_category": "自然/海洋"
}
```

## API Key 安全存储

- Key 使用 **Windows DPAPI**（`CryptProtectData` / `CryptUnprotectData`，P/Invoke `crypt32.dll`）加密后以 Base64 存入 `secrets.dat`（`AppPaths.SecretsFilePath`）。
- **不硬编码、不写入 git、不写日志、不写普通 JSON 配置。**
- 管理入口：设置页（保存/删除 Key、测试连接）。

## 图片预处理

- 原图**永不直接上传**；仅发送缩小的 JPEG 预览。
- `ImageNormalizer` 用 ImageSharp 加载 → `AutoOrient` 自动旋转 → 按 `MaxPreviewDimension`（默认 1024）等比缩小 → 以 `PreviewQuality`（默认 85）保存 JPEG 到临时目录 `temp/ai-preview-{Guid:N}.jpg`。
- 仅 JPG/PNG/WebP/BMP/GIF 可直接解码；TIFF/HEIC/RAW 需先本地解码为 JPEG/PNG 再发送（阶段一支持前五者，扩展格式后续）。
- **EXIF 仅在本地读取，绝不上传**；不做人脸识别推断真实身份。
- 隐私开关：设置页 `AllowUpload`（允许 AI 上传）默认关闭；关闭时启动分类会抛配置异常提示。

## 重试与错误处理

- 默认重试 3 次（可调 0–10），指数退避 `2^attempt + 随机(0–500ms)`。
- 可重试错误（`DeepSeekException.IsRetryable`）：限流(429)、服务器错误(≥500)、网络断开/超时。
- 错误映射（`DeepSeekErrorKind`）：

| 情况 | 类型 | 是否中止任务 |
|---|---|---|
| 401/403 | Authentication | 是（致命） |
| 402 / 余额不足（balance/insufficient/余额/资金） | InsufficientBalance | 是（致命） |
| 429 | RateLimit | 重试后中止 |
| ≥500 | Server | 重试后中止 |
| 网络异常/超时 | Network | 重试后中止 |
| 异常 JSON / 缺 content | InvalidResponse | 记失败继续 |
| 其他 | Request | 记失败继续 |

- 并发默认 3（可调 1–5），`SemaphoreSlim` 限流。

## 用量统计

- 优先使用响应中的真实 `usage`（`IsEstimated=false`）。
- 若响应无 usage，则估算：`input ≈ dataUrl.Length / 4`，`output = 100`（`IsEstimated=true`）。
- 每次请求写入 `ApiUsages`，历史页可查。

## 环境限制说明（本机实测）

本机（DSH sandbox 受限 token）曾出现 Schannel `SEC_E_NO_CREDENTIALS` 导致 .NET HTTPS 全部失败；切到 `danger-full-access` 后连通验证通过。**代码本身正确**，正常环境（非受限沙箱）直接可用。若遇 `安全包中没有可用的凭证`，属系统/沙箱 TLS 凭证问题，非应用缺陷。
