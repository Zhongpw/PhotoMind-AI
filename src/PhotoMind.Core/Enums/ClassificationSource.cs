namespace PhotoMind.Core.Enums;

/// <summary>Which engine produced a classification result.</summary>
public enum ClassificationSource
{
    /// <summary>Result produced by the DeepSeek vision API.</summary>
    DeepSeek = 0,

    /// <summary>Result produced by the local CLIP model (token-saving path).</summary>
    Clip = 1,
}
