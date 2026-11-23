namespace PhotoMind.App.ViewModels;

/// <summary>A left-navigation entry: an emoji icon, a title, and the page view model to display.</summary>
public sealed class NavigationItem
{
    public NavigationItem(string title, string icon, object content)
    {
        Title = title;
        Icon = icon;
        Content = content;
    }

    public string Title { get; }
    public string Icon { get; }
    public object Content { get; }

    public override string ToString() => Title;
}
