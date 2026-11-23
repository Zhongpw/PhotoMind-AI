using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;

namespace PhotoMind.App.ViewModels;

/// <summary>Dashboard page: library statistics, recent tasks, quick-start actions and API status.</summary>
public sealed partial class HomeViewModel : ObservableObject
{
    private readonly IScanRepository _repository;

    [ObservableProperty] private int _scannedImageCount;
    [ObservableProperty] private int _classifiedCount;
    [ObservableProperty] private int _pendingReviewCount;
    [ObservableProperty] private int _unclassifiedCount;
    [ObservableProperty] private string _apiStatusText = "未配置";
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<string> RecentDirectories { get; } = new();
    public ObservableCollection<ScanTaskItem> RecentScanTasks { get; } = new();

    public HomeViewModel(IScanRepository repository) => _repository = repository;

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            ScannedImageCount = await _repository.GetImageCountAsync().ConfigureAwait(false);

            var tasks = await _repository.GetRecentScanTasksAsync(5).ConfigureAwait(false);

            RecentScanTasks.Clear();
            foreach (var task in tasks)
                RecentScanTasks.Add(new ScanTaskItem(task));

            var directories = tasks
                .Select(t => DeserializePaths(t.SourcePath))
                .SelectMany(x => x)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5);
            RecentDirectories.Clear();
            foreach (var dir in directories)
                RecentDirectories.Add(dir);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void QuickScan() => WeakReferenceMessenger.Default.Send(new NavigateMessage("scan"));

    [RelayCommand]
    private void OpenSettings() => WeakReferenceMessenger.Default.Send(new NavigateMessage("settings"));

    [RelayCommand]
    private void ManageCategories() => WeakReferenceMessenger.Default.Send(new NavigateMessage("categories"));

    private static string[] DeserializePaths(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }
}

/// <summary>Display wrapper for a recent scan task.</summary>
public sealed class ScanTaskItem
{
    public ScanTaskItem(ScanTask task)
    {
        Id = task.Id;
        StartedText = task.StartedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
        StatusText = task.Status switch
        {
            Core.Enums.TaskState.Completed => "已完成",
            Core.Enums.TaskState.Running => "进行中",
            Core.Enums.TaskState.Paused => "已暂停",
            Core.Enums.TaskState.Failed => "失败",
            Core.Enums.TaskState.Cancelled => "已取消",
            _ => "待处理",
        };
        CountText = $"{task.ScannedCount} 张 / 失败 {task.FailedCount}";
    }

    public long Id { get; }
    public string StartedText { get; }
    public string StatusText { get; }
    public string CountText { get; }
}
