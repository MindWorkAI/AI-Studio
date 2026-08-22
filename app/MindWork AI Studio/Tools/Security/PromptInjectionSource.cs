namespace AIStudio.Tools.Security;

public readonly record struct PromptInjectionSource(string Kind, string Label)
{
    public string NotificationLabel => this.Kind is "File content" or "Chat attachment"
        ? Path.GetFileName(this.Label)
        : this.Label;

    public static PromptInjectionSource WebContent(string url) => new("Web content", url);

    public static PromptInjectionSource FileContent(string filePath) => new("File content", filePath);

    public static PromptInjectionSource ChatAttachment(string filePath) => new("Chat attachment", filePath);

    public static PromptInjectionSource RetrievalContext(string dataSourceName, string path) => new("Retrieval context", $"{dataSourceName}: {path}");
}