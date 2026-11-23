namespace PhotoMind.Core.Models;

/// <summary>A user-defined classification rule/instruction.</summary>
public sealed class UserRule
{
    public long Id { get; set; }

    public string RuleName { get; set; } = string.Empty;
    public string RuleContent { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
