namespace AIStudio.Provider.OpenAI;

public sealed record ToolResultMessage : IMessage<string>
{
    public string Role { get; set; } = "tool";

    public string Content { get; set; } = string.Empty;

    public string ToolCallId { get; set; } = string.Empty;
}
