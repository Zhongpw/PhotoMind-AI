using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;

namespace PhotoMind.App.ViewModels;

/// <summary>
/// Review page: filter classified images, view the AI description/reason, and manually correct
/// classifications (single or batch). Every change is saved to the database immediately.
/// </summary>
public sealed partial class ReviewViewModel : ObservableObject
{
    private readonly IClassificationRepository _repository;
    private IReadOnlyList<Category> _allCategories = Array.Empty<Category>();

    public ObservableCollection<ReviewItemViewModel> Items { get; } = new();
    public ObservableCollection<CategoryOption> FilterTopLevel { get; } = new();
    public ObservableCollection<CategoryOption> FilterSubCategories { get; } = new();
    public ObservableCollection<CategoryOption> BatchTopLevel { get; } = new();
    public ObservableCollection<CategoryOption> BatchSubCategories { get; } = new();

    public IReadOnlyList<ConfidenceOption> ConfidenceOptions { get; } = new[]
    {
        new ConfidenceOption(null, "全部置信度"),
        new ConfidenceOption(0.5, "≥ 50%"),
        new ConfidenceOption(0.7, "≥ 70%"),
        new ConfidenceOption(0.9, "≥ 90%"),
    };

    [ObservableProperty] private CategoryOption? _selectedFilterCategory;
    [ObservableProperty] private CategoryOption? _selectedFilterSubCategory;
    [ObservableProperty] private ConfidenceOption? _selectedConfidence;
    [ObservableProperty] private bool _needsReviewOnly;
    [ObservableProperty] private string _search = string.Empty;

    [ObservableProperty] private CategoryOption? _batchCategory;
    [ObservableProperty] private CategoryOption? _batchSubCategory;

    [ObservableProperty] private ReviewItemViewModel? _currentItem;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _reviewCount;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private string _statusText = "就绪";

    public ReviewViewModel(IClassificationRepository repository)
    {
        _repository = repository;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _allCategories = await _repository.GetCategoriesAsync().ConfigureAwait(true);

            var top = _allCategories.Where(c => c.ParentId is null).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
            var sub = _allCategories.Where(c => c.ParentId is not null).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
            var parentNames = top.ToDictionary(c => c.Id, c => c.Name);

            FilterTopLevel.Clear();
            foreach (var c in top) FilterTopLevel.Add(new CategoryOption(c, c.Name));

            FilterSubCategories.Clear();
            foreach (var c in sub) FilterSubCategories.Add(new CategoryOption(c, $"{Lookup(parentNames, c.ParentId)} / {c.Name}"));

            BatchTopLevel.Clear();
            foreach (var c in top) BatchTopLevel.Add(new CategoryOption(c, c.Name));

            BatchSubCategories.Clear();
            foreach (var c in sub) BatchSubCategories.Add(new CategoryOption(c, $"{Lookup(parentNames, c.ParentId)} / {c.Name}"));

            await ApplyFilterAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = "读取失败：" + ex.Message;
        }
    }

    private static string Lookup(IReadOnlyDictionary<long, string> names, long? parentId)
        => parentId is { } id && names.TryGetValue(id, out var name) ? name : "?";

    private ReviewFilter BuildFilter() => new()
    {
        CategoryId = SelectedFilterCategory?.Category.Id,
        SubCategoryId = SelectedFilterSubCategory?.Category.Id,
        MinConfidence = SelectedConfidence?.Value,
        NeedsReviewOnly = NeedsReviewOnly,
        Search = Search,
    };

    [RelayCommand]
    private Task RefreshCommandAsync() => LoadAsync();

    [RelayCommand]
    private Task ApplyFilterCommandAsync() => ApplyFilterAsync();

    private async Task ApplyFilterAsync()
    {
        try
        {
            var filter = BuildFilter();
            var count = await _repository.GetResultSummariesCountAsync(filter).ConfigureAwait(true);
            var rows = await _repository.GetResultSummariesAsync(filter, 0, 2000).ConfigureAwait(true);

            Items.Clear();
            foreach (var row in rows)
                Items.Add(new ReviewItemViewModel(row, RefreshSelectedCount));

            TotalCount = count;
            ReviewCount = rows.Count(r => r.NeedsReview);
            SelectedCount = 0;
            StatusText = $"共 {count:N0} 条结果，其中待审核 {ReviewCount:N0}";
        }
        catch (Exception ex)
        {
            StatusText = "查询失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private void ClearFiltersCommand()
    {
        SelectedFilterCategory = null;
        SelectedFilterSubCategory = null;
        SelectedConfidence = null;
        NeedsReviewOnly = false;
        Search = string.Empty;
        _ = ApplyFilterAsync();
    }

    public void RefreshSelectedCount() => SelectedCount = Items.Count(i => i.IsSelected);

    [RelayCommand]
    private async Task MarkReviewCommandAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText = "请先勾选图片";
            return;
        }

        await SetReviewAsync(selected, true).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MarkReviewedCommandAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText = "请先勾选图片";
            return;
        }

        await SetReviewAsync(selected, false).ConfigureAwait(true);
    }

    private async Task SetReviewAsync(List<ReviewItemViewModel> items, bool needsReview)
    {
        try
        {
            foreach (var item in items)
            {
                var result = await _repository.GetResultAsync(item.ResultId).ConfigureAwait(true);
                if (result is null) continue;
                result.NeedsReview = needsReview;
                await _repository.UpdateResultAsync(result).ConfigureAwait(true);
            }

            await ApplyFilterAsync().ConfigureAwait(true);
            StatusText = $"已{(needsReview ? "标记待审核" : "标记已审核")} {items.Count:N0} 张";
        }
        catch (Exception ex)
        {
            StatusText = "操作失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task UnclassifyCommandAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText = "请先勾选图片";
            return;
        }

        try
        {
            foreach (var item in selected)
                await _repository.DeleteResultAsync(item.ResultId).ConfigureAwait(true);

            await ApplyFilterAsync().ConfigureAwait(true);
            StatusText = $"已标记 {selected.Count:N0} 张为未分类";
        }
        catch (Exception ex)
        {
            StatusText = "操作失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task BatchAssignCommandAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText = "请先勾选图片";
            return;
        }

        if (BatchCategory is null)
        {
            StatusText = "请选择目标一级分类";
            return;
        }

        await AssignAsync(selected, BatchCategory.Category.Id, BatchSubCategory?.Category.Id).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AssignCurrentCommandAsync()
    {
        if (CurrentItem is null)
        {
            StatusText = "请先在列表中选中一张图片";
            return;
        }

        if (BatchCategory is null)
        {
            StatusText = "请选择目标一级分类";
            return;
        }

        await AssignAsync(new List<ReviewItemViewModel> { CurrentItem }, BatchCategory.Category.Id, BatchSubCategory?.Category.Id).ConfigureAwait(true);
    }

    private async Task AssignAsync(List<ReviewItemViewModel> items, long categoryId, long? subCategoryId)
    {
        try
        {
            foreach (var item in items)
            {
                var result = await _repository.GetResultAsync(item.ResultId).ConfigureAwait(true);
                if (result is null) continue;
                result.CategoryId = categoryId;
                result.SubCategoryId = subCategoryId;
                result.NeedsReview = false;
                await _repository.UpdateResultAsync(result).ConfigureAwait(true);
            }

            await ApplyFilterAsync().ConfigureAwait(true);
            StatusText = $"已修改 {items.Count:N0} 张分类";
        }
        catch (Exception ex)
        {
            StatusText = "操作失败：" + ex.Message;
        }
    }
}

/// <summary>Display wrapper for a category in a ComboBox (shows full path for sub-categories).</summary>
public sealed class CategoryOption
{
    public CategoryOption(Category category, string display)
    {
        Category = category;
        Display = display;
    }

    public Category Category { get; }
    public string Display { get; }
    public override string ToString() => Display;
}

/// <summary>Confidence filter option.</summary>
public sealed class ConfidenceOption
{
    public ConfidenceOption(double? value, string display)
    {
        Value = value;
        Display = display;
    }

    public double? Value { get; }
    public string Display { get; }
    public override string ToString() => Display;
}

/// <summary>Row shown in the review list; wraps a <see cref="ClassificationResultSummary"/>.</summary>
public sealed partial class ReviewItemViewModel : ObservableObject
{
    private readonly Action _onSelectionChanged;

    public ReviewItemViewModel(ClassificationResultSummary summary, Action onSelectionChanged)
    {
        _onSelectionChanged = onSelectionChanged;
        ResultId = summary.ResultId;
        FileName = summary.FileName;
        OriginalPath = summary.OriginalPath;
        ThumbnailPath = summary.ThumbnailPath;
        CategoryName = summary.CategoryName;
        SubCategoryName = summary.SubCategoryName;
        CategoryText = string.IsNullOrWhiteSpace(summary.SubCategoryName)
            ? (summary.CategoryName ?? "未分类")
            : $"{summary.CategoryName} / {summary.SubCategoryName}";
        ConfidenceText = summary.Confidence.ToString("P0");
        NeedsReview = summary.NeedsReview;
        Description = summary.Description;
        Reason = summary.Reason;
        TagsText = ParseTags(summary.TagsJson);
    }

    public long ResultId { get; }
    public string FileName { get; }
    public string OriginalPath { get; }
    public string? ThumbnailPath { get; }
    public string? CategoryName { get; }
    public string? SubCategoryName { get; }
    public string CategoryText { get; }
    public string ConfidenceText { get; }
    public bool NeedsReview { get; }
    public string? Description { get; }
    public string? Reason { get; }
    public string TagsText { get; }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => _onSelectionChanged();

    private static string ParseTags(string tagsJson)
    {
        try
        {
            var tags = System.Text.Json.JsonSerializer.Deserialize<string[]>(tagsJson);
            return tags is null || tags.Length == 0 ? string.Empty : string.Join("、", tags);
        }
        catch
        {
            return string.Empty;
        }
    }
}
