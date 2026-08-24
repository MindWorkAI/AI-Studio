namespace AIStudio.Chat;

public sealed record ChatStartRequest(
    ChatThread ChatThread,
    bool ApplySelectedChatTemplateToComposer = false,
    bool PreserveDataSourceOptions = false);
