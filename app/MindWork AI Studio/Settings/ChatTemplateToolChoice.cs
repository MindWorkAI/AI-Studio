namespace AIStudio.Settings;

/// <summary>
/// Whose tools a chat started by a launcher begins with, decided by ChatTemplate.ChooseToolIds.
/// </summary>
/// <param name="ToolIds">The tools to start with, or null when neither side names any, which leaves the chat default in place.</param>
/// <param name="LauncherChoiceDropped">Whether the tools the launcher names itself were dropped for those of the chat template.</param>
public readonly record struct ChatTemplateToolChoice(IReadOnlyCollection<string>? ToolIds, bool LauncherChoiceDropped)
{
    /// <summary>
    /// Neither side names any tools, so the chat keeps its default.
    /// </summary>
    public static readonly ChatTemplateToolChoice NONE = new(null, false);
}