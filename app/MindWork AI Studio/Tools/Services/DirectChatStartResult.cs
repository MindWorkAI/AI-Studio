using AIStudio.Chat;

namespace AIStudio.Tools.Services;

public sealed record DirectChatStartResult(ChatStartRequest? Request, string ErrorMessage);