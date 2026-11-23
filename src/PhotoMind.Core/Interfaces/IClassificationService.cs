using PhotoMind.Core.DTOs;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;

namespace PhotoMind.Core.Interfaces;

/// <summary>Runs an AI classification task over a set of images with pause/resume/cancel support.</summary>
public interface IClassificationService
{
    Task<ClassificationTask> ClassifyAsync(
        AiClassifyOptions options,
        PauseToken pauseToken,
        IProgress<AiClassifyProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
