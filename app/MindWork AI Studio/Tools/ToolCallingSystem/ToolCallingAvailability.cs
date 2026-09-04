namespace AIStudio.Tools.ToolCallingSystem;

public readonly record struct ToolCallingAvailability(bool IsAvailable, string Message)
{
    public static ToolCallingAvailability Available() => new(true, string.Empty);
}
