namespace AIStudio.Tools.PluginSystem.Assistants;

/// <param name="ToolIds">The tools preselected for the chat, or null when the launcher names none.</param>
public sealed record AssistantChatLaunchConfiguration(string WorkspaceName, Guid? ProviderId, Guid? ProfileId, Guid? ChatTemplateId, IReadOnlyList<Guid>? DataSourceIds, IReadOnlyList<string>? ToolIds);
