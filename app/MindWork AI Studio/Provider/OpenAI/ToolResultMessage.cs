namespace AIStudio.Provider.OpenAI;

public sealed record ToolResultMessage : IMessage<string>
{
    public string Role { get; init; } = "tool";

    public string Content { get; init; } = string.Empty;

    public string ToolCallId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
