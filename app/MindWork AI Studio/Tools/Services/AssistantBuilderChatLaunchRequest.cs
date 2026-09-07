namespace AIStudio.Tools.Services;

/// <summary>
/// The chat a direct chat launcher tile opens, as chosen in the Assistant Builder.
/// </summary>
/// <param name="WorkspaceName">The workspace the chat is created in.</param>
/// <param name="ProviderId">The provider to preselect, or null for the chat default.</param>
/// <param name="ProfileIds">The exact profiles to use, an empty list for none, or null for chat defaults.</param>
/// <param name="ChatTemplateId">The chat template to preselect; the empty GUID selects none.</param>
/// <param name="DataSourceIds">The data sources to preselect, or null for the chat defaults.</param>
/// <param name="ToolIds">The tools to preselect, or null for the chat defaults.</param>
public sealed record AssistantBuilderChatLaunchRequest(string WorkspaceName, string? ProviderId, IReadOnlyList<string>? ProfileIds, string? ChatTemplateId, IReadOnlyList<string>? DataSourceIds, IReadOnlyList<string>? ToolIds);
