using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Threading;

namespace PhotoMind.App.ViewModels;

/// <summary>
/// 照片整理页：选择目标目录与操作类型，预览后执行复制/移动/仅建目录，支持暂停/取消与撤销。
/// </summary>
public sealed partial class OrganizeViewModel : ObservableObject
{
    private readonly IOrganizeService _service;
    private readonly IOrganizeRepository _repository;

    private CancellationTokenSource? _cts;
    private PauseTokenSource? _pauseSource;
    private long? _lastTaskId;

    [ObservableProperty] private string _destinationPath = string.Empty;
    [ObservableProperty] private OrganizeOperationType _operationType = OrganizeOperationType.Copy;
    [ObservableProperty] private bool _isConfirmed;

    [ObservableProperty] private bool _isPreviewing;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private string _statusText = "就绪：请选择目标目录并预览";
    [ObservableProperty] private string _previewSummary = string.Empty;

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _failedCount;
    [ObservableProperty] private string? _currentFile;

    public IReadOnlyList<OperationOption> OperationOptions { get; } = new[]
    {
        new OperationOption(OrganizeOperationType.Copy, "复制照片（保留原文件，推荐）"),
        new OperationOption(OrganizeOperationType.Move, "移动照片（删除原文件，需二次确认）"),
        new OperationOption(OrganizeOperationType.CreateFoldersOnly, "仅创建分类目录"),
    };

    public OrganizeViewModel(IOrganizeService service, IOrganizeRepository repository)
    {
        _service = service;
        _repository = repository;
    }

    [RelayCommand]
    private void BrowseDestination()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择整理目标目录",
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            DestinationPath = dialog.FolderName;
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (IsRunning)
            return;

        try
        {
            IsPreviewing = true;
            StatusText = "正在生成预览…";

            var preview = await Task.Run(() => _service.PreviewAsync(new OrganizeOptions
            {
                DestinationPath = DestinationPath,
                OperationType = OperationType,
            })).ConfigureAwait(true);

            TotalCount = preview.TotalCount;
            PreviewSummary = BuildSummary(preview);
            StatusText = $"预览完成：{preview.TotalCount:N0} 张照片，{preview.ConflictCount:N0} 个冲突";
        }
        catch (Exception ex)
        {
            StatusText = "预览失败：" + ex.Message;
            PreviewSummary = string.Empty;
        }
        finally
        {
            IsPreviewing = false;
        }
    }

    [RelayCommand]
    private async Task StartOrganizeAsync()
    {
        if (IsRunning)
            return;
        if (!IsConfirmed)
        {
            StatusText = "请先勾选确认框再开始整理。";
            return;
        }

        TotalCount = CompletedCount = FailedCount = 0;
        CurrentFile = null;
        IsRunning = true;
        IsPaused = false;
        StatusText = "正在整理…";

        var options = new OrganizeOptions
        {
            DestinationPath = DestinationPath,
            OperationType = OperationType,
        };
        _cts = new CancellationTokenSource();
        _pauseSource = new PauseTokenSource();
        var progress = new Progress<OrganizeProgress>(OnProgress);

        try
        {
            var task = await Task.Run(() => _service.ExecuteAsync(options, _pauseSource.Token, progress, _cts.Token))
                .ConfigureAwait(true);

            _lastTaskId = task.Id;
            CanUndo = true;
            IsConfirmed = false;
            StatusText = $"整理完成：成功 {task.CompletedCount:N0}，失败 {task.FailedCount:N0}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "整理已取消";
        }
        catch (Exception ex)
        {
            StatusText = "整理失败：" + ex.Message;
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
    private void PauseOrganize()
    {
        _pauseSource?.Pause();
        IsPaused = true;
        StatusText = "已暂停";
    }

    [RelayCommand]
    private void ResumeOrganize()
    {
        _pauseSource?.Resume();
        IsPaused = false;
        StatusText = "继续整理…";
    }

    [RelayCommand]
    private void CancelOrganize()
    {
        _cts?.Cancel();
        StatusText = "正在取消…";
    }

    /// <summary>撤销最近一次整理。由 View 在用户确认后调用。</summary>
    [RelayCommand]
    private async Task UndoLastAsync()
    {
        if (IsRunning || _lastTaskId is null)
            return;

        try
        {
            StatusText = "正在撤销…";
            var result = await Task.Run(() => _service.UndoAsync(_lastTaskId.Value, default, null, CancellationToken.None))
                .ConfigureAwait(true);

            StatusText = result.Success
                ? $"撤销完成：已恢复 {result.RevertedCount:N0} 个文件"
                : $"撤销完成：成功 {result.RevertedCount:N0}，失败 {result.FailedCount:N0}";
            _lastTaskId = null;
            CanUndo = false;
        }
        catch (Exception ex)
        {
            StatusText = "撤销失败：" + ex.Message;
        }
    }

    private void OnProgress(OrganizeProgress progress)
    {
        TotalCount = progress.TotalCount;
        CompletedCount = progress.CompletedCount;
        FailedCount = progress.FailedCount;
        CurrentFile = progress.CurrentFile;

        switch (progress.Status)
        {
            case TaskState.Completed:
                StatusText = "整理完成";
                break;
            case TaskState.Failed:
                StatusText = progress.ErrorMessage ?? "整理失败";
                break;
            case TaskState.Cancelled:
                StatusText = "整理已取消";
                break;
        }
    }

    private static string BuildSummary(OrganizePreview preview)
    {
        return $"照片总数：{preview.TotalCount:N0}\n" +
               $"分类数量：{preview.CategoryCount:N0}\n" +
               $"预计创建文件夹：{preview.FolderCount:N0}\n" +
               $"预计磁盘占用：{FormatBytes(preview.TotalBytes)}\n" +
               $"文件冲突：{preview.ConflictCount:N0}\n" +
               $"错误：{preview.ErrorCount:N0}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024L * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }
}

/// <summary>ComboBox 项：整理操作类型。</summary>
public sealed class OperationOption
{
    public OperationOption(OrganizeOperationType value, string label)
    {
        Value = value;
        Label = label;
    }

    public OrganizeOperationType Value { get; }
    public string Label { get; }
    public override string ToString() => Label;
}
