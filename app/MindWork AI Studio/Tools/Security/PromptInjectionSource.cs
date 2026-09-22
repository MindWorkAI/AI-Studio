namespace AIStudio.Tools.Security;

public readonly record struct PromptInjectionSource(PromptInjectionSourceKind Kind, string Label)
{
    public string NotificationLabel => this.Kind is PromptInjectionSourceKind.FILE_CONTENT or PromptInjectionSourceKind.CHAT_ATTACHMENT
        ? Path.GetFileName(this.Label)
        : this.Label;

    public static PromptInjectionSource WebContent(string url) => new(PromptInjectionSourceKind.WEB_CONTENT, url);

    public static PromptInjectionSource FileContent(string filePath) => new(PromptInjectionSourceKind.FILE_CONTENT, filePath);

    public static PromptInjectionSource ChatAttachment(string filePath) => new(PromptInjectionSourceKind.CHAT_ATTACHMENT, filePath);

    public static PromptInjectionSource RetrievalContext(string dataSourceName, string path) => new(PromptInjectionSourceKind.RETRIEVAL_CONTEXT, $"{dataSourceName}: {path}");
}