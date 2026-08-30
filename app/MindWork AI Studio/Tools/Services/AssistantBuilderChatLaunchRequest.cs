namespace AIStudio.Tools.Services;

public sealed record AssistantBuilderChatLaunchRequest(string WorkspaceName, string? ProviderId, string? ProfileId, string? ChatTemplateId, IReadOnlyList<string>? DataSourceIds);