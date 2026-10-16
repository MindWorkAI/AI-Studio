namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolConfigurationState
{
    public bool IsConfigured { get; init; }

    public List<string> MissingRequiredFields { get; init; } = [];

    public string Message { get; init; } = string.Empty;
}