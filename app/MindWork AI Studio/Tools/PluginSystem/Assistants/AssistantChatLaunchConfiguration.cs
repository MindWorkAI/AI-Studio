namespace AIStudio.Tools.PluginSystem.Assistants;

/// <param name="ProfileIds">The exact profiles to use, an empty list for none, or null for chat defaults.</param>
/// <param name="ToolIds">The tools preselected for the chat, or null when the launcher names none.</param>
public sealed record AssistantChatLaunchConfiguration(string WorkspaceName, Guid? ProviderId, IReadOnlyList<Guid>? ProfileIds, Guid? ChatTemplateId, IReadOnlyList<Guid>? DataSourceIds, IReadOnlyList<string>? ToolIds);
