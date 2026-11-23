using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;

namespace PhotoMind.App.ViewModels;

/// <summary>History page: recent scan/classification/organise tasks, duplicate images and API usage.</summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IScanRepository _scanRepository;
    private readonly IClassificationRepository _classificationRepository;
    private readonly IOrganizeRepository _organizeRepository;

    public ObservableCollection<TaskHistoryRow> Tasks { get; } = new();
    public ObservableCollection<DuplicateGroupRow> Duplicates { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private string _usageText = "—";
    [ObservableProperty] private string _duplicateCountText = "—";

    public HistoryViewModel(
        IScanRepository scanRepository,
        IClassificationRepository classificationRepository,
        IOrganizeRepository organizeRepository)
    {
        _scanRepository = scanRepository;
        _classificationRepository = classificationRepository;
        _organizeRepository = organizeRepository;
        _ = InitializeAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await InitializeAsync().ConfigureAwait(true);

    private async Task InitializeAsync()
    {
        IsLoading = true;
        StatusText = "正在加载历史记录…";
        try
        {
            var scanTasks = await _scanRepository.GetRecentScanTasksAsync(20).ConfigureAwait(false);
            var classificationTasks = await _classificationRepository.GetRecentClassificationTasksAsync(20).ConfigureAwait(false);
            var organizeTasks = await _organizeRepository.GetRecentOrganizeTasksAsync(20).ConfigureAwait(false);
            var duplicateGroups = await _scanRepository.GetDuplicateGroupsAsync().ConfigureAwait(false);
            var usage = await _classificationRepository.GetUsageSummaryAsync().ConfigureAwait(false);

            var rows = new List<TaskHistoryRow>(scanTasks.Count + classificationTasks.Count + organizeTasks.Count);
            foreach (var t in scanTasks) rows.Add(TaskHistoryRow.Scan(t));
            foreach (var t in classificationTasks) rows.Add(TaskHistoryRow.Classification(t));
            foreach (var t in organizeTasks) rows.Add(TaskHistoryRow.Organize(t));
            rows.Sort((a, b) => b.SortTime.CompareTo(a.SortTime));

            Tasks.Clear();
            foreach (var r in rows) Tasks.Add(r);

            Duplicates.Clear();
            foreach (var g in duplicateGroups) Duplicates.Add(new DuplicateGroupRow(g));

            UsageText = usage.RequestCount == 0
                ? "暂无 API 调用记录"
                : $"请求 {usage.RequestCount} 次 · Token 输入 {usage.InputTokens:N0} / 输出 {usage.OutputTokens:N0} / 总计 {usage.TotalTokens:N0}"
                  + (usage.EstimatedCount > 0 ? $" · 其中 {usage.EstimatedCount} 次为估算值" : "");

            DuplicateCountText = Duplicates.Count == 0
                ? "未发现重复图片"
                : $"发现 {Duplicates.Count} 组重复图片（仅提示，不会自动删除）";

            StatusText = Tasks.Count == 0 ? "暂无历史任务" : null;
        }
        catch (Exception ex)
        {
            StatusText = "加载历史记录失败：" + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>A unified row for one recent task of any kind (scan / classify / organise).</summary>
public sealed class TaskHistoryRow
{
    public string Type { get; init; } = string.Empty;
    public long Id { get; init; }
    public string Detail { get; init; } = "—";
    public string Progress { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Started { get; init; } = "—";
    public DateTime SortTime { get; init; }

    public static TaskHistoryRow Scan(ScanTask t) => new()
    {
        Type = "扫描",
        Id = t.Id,
        Detail = SummarizeSources(t.SourcePath),
        Progress = $"{t.ScannedCount}/{t.TotalCount}",
        Status = StatusText(t.Status),
        Started = FormatTime(t.StartedAt),
        SortTime = t.StartedAt ?? DateTime.MinValue,
    };

    public static TaskHistoryRow Classification(ClassificationTask t) => new()
    {
        Type = "分类",
        Id = t.Id,
        Detail = "AI 智能分类",
        Progress = $"{t.CompletedCount}/{t.TotalCount}",
        Status = StatusText(t.Status),
        Started = FormatTime(t.StartedAt),
        SortTime = t.StartedAt ?? DateTime.MinValue,
    };

    public static TaskHistoryRow Organize(OrganizeTask t) => new()
    {
        Type = "整理",
        Id = t.Id,
        Detail = OperationText(t.OperationType) + " → " + t.DestinationPath,
        Progress = $"{t.CompletedCount}/{t.TotalCount}",
        Status = StatusText(t.Status),
        Started = FormatTime(t.StartedAt),
        SortTime = t.StartedAt ?? DateTime.MinValue,
    };

    private static string StatusText(TaskState s) => s switch
    {
        TaskState.Pending => "待处理",
        TaskState.Running => "运行中",
        TaskState.Paused => "已暂停",
        TaskState.Completed => "已完成",
        TaskState.Failed => "失败",
        TaskState.Cancelled => "已取消",
        _ => s.ToString(),
    };

    private static string OperationText(OrganizeOperationType o) => o switch
    {
        OrganizeOperationType.None => "不整理",
        OrganizeOperationType.CreateFoldersOnly => "仅创建目录",
        OrganizeOperationType.Copy => "复制",
        OrganizeOperationType.Move => "移动",
        _ => o.ToString(),
    };

    private static string FormatTime(DateTime? dt) => dt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "—";

    private static string SummarizeSources(string sourcePath)
    {
        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(sourcePath);
            if (arr is { Length: > 0 })
                return arr.Length == 1 ? arr[0] : $"{arr[0]} 等 {arr.Length} 处";
        }
        catch
        {
            // fall through
        }
        return sourcePath;
    }
}

/// <summary>A duplicate group shown in the history page (prompt only — never auto-deletes).</summary>
public sealed class DuplicateGroupRow
{
    public string Hash { get; }
    public int Count { get; }
    public string Paths { get; }

    public DuplicateGroupRow(DuplicateImageGroup group)
    {
        Hash = group.FileHash.Length > 12 ? group.FileHash[..12] + "…" : group.FileHash;
        Count = group.Count;
        var shown = group.Images.Take(3).Select(i => i.OriginalPath);
        Paths = string.Join("\n", shown) + (group.Count > 3 ? $"\n… 等 {group.Count} 张" : string.Empty);
    }
}
