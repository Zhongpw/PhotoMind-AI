namespace PhotoMind.Core.Enums;

/// <summary>The device actually used for CLIP inference.</summary>
public enum InferenceDevice
{
    /// <summary>CPU execution provider.</summary>
    Cpu = 0,

    /// <summary>NVIDIA CUDA execution provider.</summary>
    Cuda = 1,
}
