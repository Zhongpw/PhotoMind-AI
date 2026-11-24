using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure.FileSystem;

namespace PhotoMind.App.ViewModels;

/// <summary>
/// Scan page: source selection (folders/disks), exclusions, format filters, live progress, pause/resume/cancel
/// and a virtualised thumbnail grid of the results. Original files are only ever read.
/// </summary>
public sealed partial class ScanViewModel : ObservableObject
{
    private readonly IScanService _scanService;
    private readonly IScanRepository _repository;
    private readonly DirectoryScanner _scanner;

    private CancellationTokenSource? _cts;
    private PauseTokenSource? _pauseSource;

    public ObservableCollection<string> SourceDirectories { get; } = new();
    public ObservableCollection<string> ExcludedDirectories { get; } = new();
    public ObservableCollection<FormatFilterItem> FormatFilters { get; } = new();
    public ObservableCollection<string> AvailableDrives { get; } = new();
    public ObservableCollection<ImageItemViewModel> Images { get; private set; } = new();
    public ObservableCollection<ScanError> Errors { get; } = new();

    [ObservableProperty] private bool _recursive = true;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _statusText = "就绪：请选择要扫描的文件夹或磁盘";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _scannedCount;
    [ObservableProperty] private int _failedCount;
    [ObservableProperty] private string? _currentFile;
    [ObservableProperty] private long? _estimatedCount;
    [ObservableProperty] private string? _selectedDrive;

    public ScanViewModel(IScanService scanService, IScanRepository repository, DirectoryScanner scanner)
    {
        _scanService = scanService;
        _repository = repository;
        _scanner = scanner;

        FormatFilters.Add(new FormatFilterItem(ImageFormat.Jpeg, "JPG / JPEG", true));
        FormatFilters.Add(new FormatFilterItem(ImageFormat.Png, "PNG", true));
        FormatFilters.Add(new FormatFilterItem(ImageFormat.WebP, "WebP", true));
        FormatFilters.Add(new FormatFilterItem(ImageFormat.Bmp, "BMP", true));
        FormatFilters.Add(new FormatFilterItem(ImageFormat.Gif, "GIF", true));

        foreach (var drive in DriveInfo.GetDrives())
        {
            if ((drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable) && drive.IsReady)
                AvailableDrives.Add(drive.RootDirectory.FullName);
        }

        SelectedDrive = AvailableDrives.FirstOrDefault();
    }

    public bool HasSources => SourceDirectories.Count > 0;

    [RelayCommand]
    private void AddFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择要扫描的文件夹",
            Multiselect = true,
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var folder in dialog.FolderNames)
            {
                if (!string.IsNullOrWhiteSpace(folder) && !SourceDirectories.Contains(folder, StringComparer.OrdinalIgnoreCase))
                    SourceDirectories.Add(folder);
            }
            OnPropertyChanged(nameof(HasSources));
        }
    }

    [RelayCommand]
    private void AddDisk()
    {
        if (string.IsNullOrWhiteSpace(SelectedDrive))
            return;

        if (!SourceDirectories.Contains(SelectedDrive, StringComparer.OrdinalIgnoreCase))
            SourceDirectories.Add(SelectedDrive);
        OnPropertyChanged(nameof(HasSources));
    }

    [RelayCommand]
    private void RemoveSource(string? path)
    {
        if (path is not null)
            SourceDirectories.Remove(path);
        OnPropertyChanged(nameof(HasSources));
    }

    [RelayCommand]
    private void AddExclude()
    {
        var dialog = new OpenFolderDialog { Title = "选择要排除的文件夹", Multiselect = true };
        if (dialog.ShowDialog() == true)
        {
            foreach (var folder in dialog.FolderNames)
            {
                if (!string.IsNullOrWhiteSpace(folder) && !ExcludedDirectories.Contains(folder, StringComparer.OrdinalIgnoreCase))
                    ExcludedDirectories.Add(folder);
            }
        }
    }

    [RelayCommand]
    private void RemoveExclude(string? path)
    {
        if (path is not null)
            ExcludedDirectories.Remove(path);
    }

    [RelayCommand]
    private async Task EstimateAsync()
    {
        if (SourceDirectories.Count == 0)
            return;

        StatusText = "正在估算文件数量…";
        try
        {
            EstimatedCount = await Task.Run(() =>
                _scanner.EstimateCountAsync(SourceDirectories.ToList(), ExcludedDirectories.ToList(), Recursive)).ConfigureAwait(true);
            StatusText = EstimatedCount is null ? "无法估算" : $"预估约 {EstimatedCount:N0} 张图片";
        }
        catch (Exception ex)
        {
            StatusText = "估算失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (SourceDirectories.Count == 0)
        {
            StatusText = "请先添加要扫描的文件夹或磁盘";
            return;
        }

        Images = new ObservableCollection<ImageItemViewModel>();
        OnPropertyChanged(nameof(Images));
        Errors.Clear();
        TotalCount = ScannedCount = FailedCount = 0;
        CurrentFile = null;
        EstimatedCount = null;
        IsScanning = true;
        IsPaused = false;
        StatusText = "正在扫描…";

        var options = BuildOptions();
        _cts = new CancellationTokenSource();
        _pauseSource = new PauseTokenSource();
        var progress = new Progress<ScanProgress>(OnProgress);

        try
        {
            var task = await Task.Run(() => _scanService.ScanAsync(options, _pauseSource.Token, progress, _cts.Token))
                .ConfigureAwait(true);

            StatusText = $"扫描完成：共 {task.ScannedCount:N0} 张，失败 {task.FailedCount:N0} 张";
            await LoadImagesAsync(task.Id).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消";
        }
        catch (Exception ex)
        {
            StatusText = "扫描失败：" + ex.Message;
        }
        finally
        {
            IsScanning = false;
            IsPaused = false;
            _cts?.Dispose();
            _cts = null;
            _pauseSource = null;
        }
    }

    [RelayCommand]
    private void PauseScan()
    {
        _pauseSource?.Pause();
        IsPaused = true;
        StatusText = "已暂停";
    }

    [RelayCommand]
    private void ResumeScan()
    {
        _pauseSource?.Resume();
        IsPaused = false;
        StatusText = "继续扫描…";
    }

    [RelayCommand]
    private void CancelScan()
    {
        _cts?.Cancel();
        StatusText = "正在取消…";
    }

    private ScanOptions BuildOptions()
    {
        var options = new ScanOptions
        {
            Recursive = Recursive,
            SourcePaths = SourceDirectories.ToList(),
            ExcludedDirectories = ExcludedDirectories.ToList(),
        };

        var selected = FormatFilters.Where(f => f.IsChecked).Select(f => f.Format).ToList();
        if (selected.Count > 0 && selected.Count < FormatFilters.Count)
            options.AllowedFormats = selected;

        return options;
    }

    private void OnProgress(ScanProgress progress)
    {
        TotalCount = progress.TotalCount;
        ScannedCount = progress.ScannedCount;
        FailedCount = progress.FailedCount;
        CurrentFile = progress.CurrentFile;

        for (int i = Errors.Count; i < progress.Errors.Count; i++)
            Errors.Add(progress.Errors[i]);

        if (progress.Status == TaskState.Completed)
            StatusText = "扫描完成，正在加载结果…";
        else if (progress.Status == TaskState.Failed)
            StatusText = "扫描失败";
        else if (progress.Status == TaskState.Cancelled)
            StatusText = "扫描已取消";
    }

    private async Task LoadImagesAsync(long taskId)
    {
        var entities = await _repository.GetImagesByTaskAsync(taskId).ConfigureAwait(true);
        var items = entities.Select(e => new ImageItemViewModel(e)).ToList();
        Images = new ObservableCollection<ImageItemViewModel>(items);
        OnPropertyChanged(nameof(Images));
        StatusText = $"已加载 {items.Count:N0} 张图片";
    }

    /// <summary>
    /// UI entry point (context menu): confirms with the user, then removes the selected images from the
    /// current task queue. Only the queue membership is affected — global records, caches and originals are untouched.
    /// </summary>
    public async Task RemoveFromTaskAsync(IReadOnlyList<ImageItemViewModel> items)
    {
        if (items.Count == 0)
            return;

        var count = items.Count;
        var confirm = MessageBox.Show(
            $"确定要从任务列表移除 {count} 张图片吗？\n\n原始图片、全局图片记录和已有分类结果都不会被删除或修改。",
            "从任务列表移除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var removed = await RemoveFromTaskCoreAsync(items.Select(i => i.Id).ToList()).ConfigureAwait(true);
            StatusText = removed > 0
                ? $"已从任务列表移除 {removed:N0} 张图片（不影响原图与全局记录）"
                : "移除未生效：这些图片可能已不在任务队列中";
        }
        catch (Exception ex)
        {
            StatusText = "移除失败：" + ex.Message;
        }
    }

    /// <summary>Testable core: persists the skipped state and refreshes the in-memory list immediately.</summary>
    public async Task<int> RemoveFromTaskCoreAsync(IReadOnlyList<long> imageIds)
    {
        if (imageIds.Count == 0)
            return 0;

        var removed = await _repository.MarkImagesSkippedAsync(imageIds).ConfigureAwait(true);

        if (removed > 0)
        {
            var ids = new HashSet<long>(imageIds);
            for (int i = Images.Count - 1; i >= 0; i--)
            {
                if (ids.Contains(Images[i].Id))
                    Images.RemoveAt(i);
            }
        }

        return removed;
    }
}
