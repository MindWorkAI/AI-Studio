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

    public static PromptInjectionSource DataSourceDescription(string dataSourceName) => new(PromptInjectionSourceKind.DATA_SOURCE_DESCRIPTION, dataSourceName);

    /// <summary>
    /// The content of the mails of one mailbox, its header fields included.
    /// </summary>
    /// <remarks>
    /// Named after the mailbox alone, never after a mail: the label ends up in the log, which keeps
    /// no subjects and no addresses. It also makes all mails of a mailbox one source, so a sync
    /// which filtered ten mails reports one mailbox instead of ten mails.
    /// </remarks>
    /// <param name="mailboxName">The name of the mailbox.</param>
    public static PromptInjectionSource MailContent(string mailboxName) => new(PromptInjectionSourceKind.MAIL_CONTENT, mailboxName);
}