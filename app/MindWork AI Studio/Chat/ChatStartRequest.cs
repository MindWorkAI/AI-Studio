namespace AIStudio.Chat;

public sealed record ChatStartRequest(ChatThread ChatThread, bool PreserveDataSourceOptions = false);