using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;

namespace PhotoMind.App.ViewModels;

/// <summary>
/// Category management page: browse the category tree, add/rename/delete categories and sub-categories.
/// Built-in (system) categories cannot be deleted but may be renamed.
/// </summary>
public sealed partial class CategoryManagementViewModel : ObservableObject
{
    private readonly IClassificationRepository _repository;

    public ObservableCollection<CategoryNodeViewModel> TopLevelCategories { get; } = new();

    [ObservableProperty] private CategoryNodeViewModel? _selectedNode;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newDescription = string.Empty;
    [ObservableProperty] private string _statusText = "就绪";

    public CategoryManagementViewModel(IClassificationRepository repository)
    {
        _repository = repository;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var all = await _repository.GetCategoriesAsync().ConfigureAwait(true);
            TopLevelCategories.Clear();

            var top = all.Where(c => c.ParentId is null).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
            foreach (var parent in top)
            {
                var node = new CategoryNodeViewModel(parent);
                foreach (var child in all.Where(c => c.ParentId == parent.Id).OrderBy(c => c.SortOrder).ThenBy(c => c.Id))
                    node.Children.Add(new CategoryNodeViewModel(child));
                TopLevelCategories.Add(node);
            }

            StatusText = $"共 {all.Count:N0} 个分类";
        }
        catch (Exception ex)
        {
            StatusText = "读取分类失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private Task RefreshCommandAsync() => RefreshAsync();

    [RelayCommand]
    private async Task AddTopLevelAsync()
    {
        var name = NewName.Trim();
        if (name.Length == 0)
        {
            StatusText = "请输入分类名称";
            return;
        }

        try
        {
            if (await _repository.GetCategoryByNameAsync(name).ConfigureAwait(true) is not null)
            {
                StatusText = "分类已存在";
                return;
            }

            var all = await _repository.GetCategoriesAsync().ConfigureAwait(true);
            var maxOrder = all.Where(c => c.ParentId is null).Select(c => c.SortOrder).DefaultIfEmpty(-1).Max();

            await _repository.AddCategoryAsync(new Category
            {
                Name = name,
                Description = NewDescription.Trim(),
                IsSystem = false,
                SortOrder = maxOrder + 1,
                CreatedAt = DateTime.UtcNow,
            }).ConfigureAwait(true);

            NewName = string.Empty;
            NewDescription = string.Empty;
            await RefreshAsync().ConfigureAwait(true);
            StatusText = "已添加分类";
        }
        catch (Exception ex)
        {
            StatusText = "添加失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddSubCategoryAsync()
    {
        if (SelectedNode is null)
        {
            StatusText = "请先选择一个父分类";
            return;
        }

        var name = NewName.Trim();
        if (name.Length == 0)
        {
            StatusText = "请输入分类名称";
            return;
        }

        try
        {
            if (await _repository.GetCategoryByNameAsync(name).ConfigureAwait(true) is not null)
            {
                StatusText = "分类已存在";
                return;
            }

            var siblings = await _repository.GetChildCategoriesAsync(SelectedNode.Id).ConfigureAwait(true);
            var maxOrder = siblings.Select(c => c.SortOrder).DefaultIfEmpty(-1).Max();

            await _repository.AddCategoryAsync(new Category
            {
                Name = name,
                Description = NewDescription.Trim(),
                ParentId = SelectedNode.Id,
                IsSystem = false,
                SortOrder = maxOrder + 1,
                CreatedAt = DateTime.UtcNow,
            }).ConfigureAwait(true);

            NewName = string.Empty;
            NewDescription = string.Empty;
            await RefreshAsync().ConfigureAwait(true);
            StatusText = "已添加子分类";
        }
        catch (Exception ex)
        {
            StatusText = "添加失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        if (SelectedNode is null)
        {
            StatusText = "请先选择一个分类";
            return;
        }

        var name = NewName.Trim();
        if (name.Length == 0)
        {
            StatusText = "请输入新的分类名称";
            return;
        }

        try
        {
            var category = await _repository.GetCategoryAsync(SelectedNode.Id).ConfigureAwait(true);
            if (category is null)
            {
                StatusText = "分类不存在";
                return;
            }

            category.Name = name;
            if (NewDescription.Trim().Length > 0)
                category.Description = NewDescription.Trim();

            await _repository.UpdateCategoryAsync(category).ConfigureAwait(true);

            NewName = string.Empty;
            NewDescription = string.Empty;
            await RefreshAsync().ConfigureAwait(true);
            StatusText = "已重命名";
        }
        catch (Exception ex)
        {
            StatusText = "重命名失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedNode is null)
        {
            StatusText = "请先选择一个分类";
            return;
        }

        if (SelectedNode.IsSystem)
        {
            StatusText = "系统内置分类不可删除（可重命名）";
            return;
        }

        try
        {
            var children = await _repository.GetChildCategoriesAsync(SelectedNode.Id).ConfigureAwait(true);
            if (children.Count > 0)
            {
                StatusText = "该分类下还有子分类，请先删除子分类";
                return;
            }

            await _repository.DeleteCategoryAsync(SelectedNode.Id).ConfigureAwait(true);
            SelectedNode = null;
            await RefreshAsync().ConfigureAwait(true);
            StatusText = "已删除";
        }
        catch (Exception ex)
        {
            StatusText = "删除失败：" + ex.Message;
        }
    }
}

/// <summary>Node in the category tree shown on the category management page.</summary>
public sealed class CategoryNodeViewModel
{
    public CategoryNodeViewModel(Category category)
    {
        Id = category.Id;
        Name = category.Name;
        Description = category.Description;
        IsSystem = category.IsSystem;
        DisplayName = category.IsSystem ? $"{category.Name}（内置）" : category.Name;
    }

    public long Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool IsSystem { get; }
    public string DisplayName { get; }
    public ObservableCollection<CategoryNodeViewModel> Children { get; } = new();
}
