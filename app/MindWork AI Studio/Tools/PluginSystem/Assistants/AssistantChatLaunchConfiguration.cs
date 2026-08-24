namespace AIStudio.Tools.PluginSystem.Assistants;

public sealed record AssistantChatLaunchConfiguration(
    string WorkspaceName,
    Guid? ProviderId,
    Guid? ProfileId,
    Guid? ChatTemplateId,
    IReadOnlyList<Guid>? DataSourceIds);
