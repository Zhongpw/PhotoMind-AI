using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Threading;

namespace PhotoMind.App.ViewModels;

/// <summary>
/// AI classification page: pick a mode, run classification over not-yet-classified images with
/// pause/resume/cancel, and show the latest results.
/// </summary>
public sealed partial class ClassificationViewModel : ObservableObject
{
    private readonly IClassificationService _service;
    private readonly IClassificationRepository _repository;

    private CancellationTokenSource? _cts;
    private PauseTokenSource? _pauseSource;

    public ObservableCollection<ClassificationResultItemViewModel> Results { get; } = new();

    [ObservableProperty] private ClassificationMode _mode = ClassificationMode.Auto;
    [ObservableProperty] private bool _includeAlreadyClassified;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _statusText = "就绪：请先在「设置」中配置 API Key";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _failedCount;
    [ObservableProperty] private string? _currentFile;
    [ObservableProperty] private int _unclassifiedCount;

    public IReadOnlyList<ModeOption> ModeOptions { get; } = new[]
    {
        new ModeOption(ClassificationMode.Auto, "AI 自动分类"),
        new ModeOption(ClassificationMode.Fixed, "用户自定义分类"),
        new ModeOption(ClassificationMode.Hybrid, "混合分类"),
    };

    public ClassificationViewModel(IClassificationService service, IClassificationRepository repository)
    {
        _service = service;
        _repository = repository;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var ids = await _repository.GetUnclassifiedImageIdsAsync(int.MaxValue).ConfigureAwait(true);
            UnclassifiedCount = ids.Count;
            await LoadResultsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = "读取数据失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private Task RefreshCommandAsync() => RefreshAsync();

    [RelayCommand]
    private async Task StartClassifyAsync()
    {
        if (IsRunning)
            return;

        Results.Clear();
        TotalCount = CompletedCount = FailedCount = 0;
        CurrentFile = null;
        IsRunning = true;
        IsPaused = false;
        StatusText = "正在分类…";

        var options = new AiClassifyOptions
        {
            Mode = Mode,
            IncludeAlreadyClassified = IncludeAlreadyClassified,
        };
        _cts = new CancellationTokenSource();
        _pauseSource = new PauseTokenSource();
        var progress = new Progress<AiClassifyProgress>(OnProgress);

        try
        {
            var task = await Task.Run(() => _service.ClassifyAsync(options, _pauseSource.Token, progress, _cts.Token))
                .ConfigureAwait(true);

            StatusText = $"分类完成：成功 {task.CompletedCount:N0}，失败 {task.FailedCount:N0}";
            await LoadResultsAsync().ConfigureAwait(true);

            var remaining = await _repository.GetUnclassifiedImageIdsAsync(int.MaxValue).ConfigureAwait(true);
            UnclassifiedCount = remaining.Count;
        }
        catch (OperationCanceledException)
        {
            StatusText = "分类已取消";
        }
        catch (Exception ex)
        {
            StatusText = "分类失败：" + ex.Message;
        }
        finally
        {
            IsRunning = false;
            IsPaused = false;
            _cts?.Dispose();
            _cts = null;
            _pauseSource = null;
        }
    }

    [RelayCommand]
    private void PauseClassify()
    {
        _pauseSource?.Pause();
        IsPaused = true;
        StatusText = "已暂停";
    }

    [RelayCommand]
    private void ResumeClassify()
    {
        _pauseSource?.Resume();
        IsPaused = false;
        StatusText = "继续分类…";
    }

    [RelayCommand]
    private void CancelClassify()
    {
        _cts?.Cancel();
        StatusText = "正在取消…";
    }

    private void OnProgress(AiClassifyProgress progress)
    {
        TotalCount = progress.TotalCount;
        CompletedCount = progress.CompletedCount;
        FailedCount = progress.FailedCount;
        CurrentFile = progress.CurrentFile;

        switch (progress.Status)
        {
            case TaskState.Completed:
                StatusText = "分类完成，正在加载结果…";
                break;
            case TaskState.Failed:
                StatusText = progress.ErrorMessage ?? "分类失败";
                break;
            case TaskState.Cancelled:
                StatusText = "分类已取消";
                break;
        }
    }

    private async Task LoadResultsAsync()
    {
        var summaries = await _repository.GetRecentResultSummariesAsync(500).ConfigureAwait(true);
        Results.Clear();
        foreach (var summary in summaries)
            Results.Add(new ClassificationResultItemViewModel(summary));
    }
}

/// <summary>ComboBox item for the classification mode.</summary>
public sealed class ModeOption
{
    public ModeOption(ClassificationMode value, string label)
    {
        Value = value;
        Label = label;
    }

    public ClassificationMode Value { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

/// <summary>Read-only row shown in the classification results list.</summary>
public sealed class ClassificationResultItemViewModel
{
    public ClassificationResultItemViewModel(ClassificationResultSummary summary)
    {
        FileName = summary.FileName;
        OriginalPath = summary.OriginalPath;
        ThumbnailPath = summary.ThumbnailPath;
        CategoryText = string.IsNullOrWhiteSpace(summary.SubCategoryName)
            ? (summary.CategoryName ?? "未分类")
            : $"{summary.CategoryName} / {summary.SubCategoryName}";
        ConfidenceText = summary.Confidence.ToString("P0");
        NeedsReview = summary.NeedsReview;
        Description = summary.Description;
    }

    public string FileName { get; }
    public string OriginalPath { get; }
    public string? ThumbnailPath { get; }
    public string CategoryText { get; }
    public string ConfidenceText { get; }
    public bool NeedsReview { get; }
    public string? Description { get; }
}
