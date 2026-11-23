using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace PhotoMind.App.ViewModels;

/// <summary>
/// Hosts the left navigation and the current page content. Navigation is driven by selecting an item or by
/// sending a <see cref="NavigateMessage"/> from any page (e.g. the Home quick-start buttons).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    [ObservableProperty]
    private object? _currentViewModel;

    public MainViewModel(
        HomeViewModel home,
        ScanViewModel scan,
        ClassificationViewModel classify,
        ReviewViewModel review,
        CategoryManagementViewModel categoryManagement,
        SettingsViewModel settings)
    {
        NavigationItems.Add(new NavigationItem("首页", "🏠", home));
        NavigationItems.Add(new NavigationItem("图片扫描", "🖼️", scan));
        NavigationItems.Add(new NavigationItem("AI 智能分类", "🤖", classify));
        NavigationItems.Add(new NavigationItem("分类审核", "✅", review));
        NavigationItems.Add(new NavigationItem("照片整理", "📁", new PlaceholderViewModel("照片整理")));
        NavigationItems.Add(new NavigationItem("分类管理", "🏷️", categoryManagement));
        NavigationItems.Add(new NavigationItem("历史任务", "🕘", new PlaceholderViewModel("历史任务")));
        NavigationItems.Add(new NavigationItem("设置", "⚙️", settings));

        WeakReferenceMessenger.Default.Register<NavigateMessage>(this, (_, message) => NavigateToKey(message.PageKey));

        SelectedNavigationItem = NavigationItems[0];
        CurrentViewModel = SelectedNavigationItem.Content;
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value is not null)
            CurrentViewModel = value.Content;
    }

    private void NavigateToKey(string pageKey)
    {
        var target = NavigationItems.FirstOrDefault(x => x.Title switch
        {
            "首页" => pageKey == "home",
            "图片扫描" => pageKey == "scan",
            "AI 智能分类" => pageKey == "classify",
            "分类审核" => pageKey == "review",
            "照片整理" => pageKey == "organize",
            "分类管理" => pageKey == "categories",
            "历史任务" => pageKey == "history",
            "设置" => pageKey == "settings",
            _ => false,
        });

        if (target is not null)
            SelectedNavigationItem = target;
    }
}

/// <summary>Navigation request carried on the WeakReferenceMessenger bus.</summary>
public sealed record NavigateMessage(string PageKey);
