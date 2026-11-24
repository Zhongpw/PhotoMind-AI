namespace PhotoMind.Core.Enums;

/// <summary>Load/health state of the local CLIP model.</summary>
public enum ClipModelState
{
    /// <summary>Model has not been requested yet.</summary>
    NotLoaded = 0,

    /// <summary>Model is loading/initializing.</summary>
    Loading = 1,

    /// <summary>Model is loaded and ready for inference.</summary>
    Ready = 2,

    /// <summary>Model failed to load; DeepSeek fallback must be used.</summary>
    Failed = 3,
}
