namespace PhotoMind.Core.Enums;

/// <summary>Classification strategy: how strictly the AI must follow the existing category tree.</summary>
public enum ClassificationMode
{
    /// <summary>AI picks categories freely and may suggest new ones.</summary>
    Auto = 0,

    /// <summary>AI may only choose from the user's existing categories.</summary>
    Fixed = 1,

    /// <summary>AI prefers existing categories but may propose new ones for user approval.</summary>
    Hybrid = 2,
}
