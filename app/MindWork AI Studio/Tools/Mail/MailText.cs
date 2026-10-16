namespace AIStudio.Tools.Mail;

/// <summary>
/// One mail as text, the way AI Studio indexes it and a model gets to read it.
/// </summary>
/// <param name="HeaderBlock">The lines written above the text: who wrote to whom, when, and about what.</param>
/// <param name="Body">The text of the mail, empty when it is encrypted or has none.</param>
/// <param name="Subject">The subject on one line, empty when the mail has none.</param>
/// <param name="EncryptionKind">How the content is encrypted, NONE when it is readable.</param>
/// <param name="Importance">How important the sender marked the mail.</param>
/// <param name="BodySource">Which part of the mail the text was read from.</param>
public sealed record MailText(string HeaderBlock, string Body, string Subject, MailEncryptionKind EncryptionKind, MailImportance Importance, MailBodySource BodySource)
{
    /// <summary>
    /// The header block and the text together, which is what gets cut into chunks: the first
    /// chunk starts with the header block, so a search for a sender or a subject finds the mail.
    /// </summary>
    public string FullText => this.Body.Length is 0 ? this.HeaderBlock : $"{this.HeaderBlock}\n\n{this.Body}";
}