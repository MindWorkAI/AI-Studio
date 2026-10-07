namespace AIStudio.Tools.Services;

public sealed record AssistantPluginLuaGenerationRequest(
    Guid PluginId,
    string ApprovedAssistantDraft,
    string ReviewNotes,
    AssistantBuilderChatLaunchRequest? ChatLaunch);