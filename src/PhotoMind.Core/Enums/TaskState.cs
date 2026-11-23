namespace PhotoMind.Core.Enums;

/// <summary>Lifecycle status shared by all long-running tasks (scan / classify / organize).</summary>
public enum TaskState
{
    Pending = 0,
    Running = 1,
    Paused = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
}
