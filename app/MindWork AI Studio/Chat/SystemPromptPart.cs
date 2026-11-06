using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Chat;

/// <summary>
/// One part of the system prompt, as it stands in the text sent to the provider.
/// </summary>
/// <remarks>
/// Recorded by ChatThread.BuildSystemPrompt while it assembles the text, so that whoever shows the
/// prompt learns where each paragraph came from from the one place which decided it, instead of
/// working it out again.
/// </remarks>
/// <param name="Kind">Where the part comes from.</param>
/// <param name="Text">The text of the part, exactly as it stands in the system prompt.</param>
public sealed record SystemPromptPart(SystemPromptPartKind Kind, string Text)
{
    /// <summary>
    /// The name of the chat template or the profile the part comes from; empty for every other part.
    /// </summary>
    public string SourceName { get; init; } = string.Empty;

    /// <summary>
    /// The tools whose usage policy the part states; empty for every other part.
    /// </summary>
    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];
}