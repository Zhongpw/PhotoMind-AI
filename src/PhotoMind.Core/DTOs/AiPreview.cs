namespace PhotoMind.Core.DTOs;

/// <summary>A locally generated, temporary preview image ready to be base64-encoded for the AI.</summary>
public sealed record AiPreview(string TempFilePath, string MimeType);
