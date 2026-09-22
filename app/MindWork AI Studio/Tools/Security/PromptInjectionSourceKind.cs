namespace AIStudio.Tools.Security;

public enum PromptInjectionSourceKind
{
    UNKNOWN = 0,
    WEB_CONTENT,
    FILE_CONTENT,
    CHAT_ATTACHMENT,
    RETRIEVAL_CONTEXT,
}