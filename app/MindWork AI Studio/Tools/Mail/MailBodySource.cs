namespace AIStudio.Tools.Mail;

/// <summary>
/// Which part of a mail its text was read from.
/// </summary>
public enum MailBodySource
{
    /// <summary>
    /// No part: the mail is encrypted, or it has no text at all.
    /// </summary>
    NONE,

    /// <summary>
    /// The HTML part, which is what a reader of the mail sees.
    /// </summary>
    HTML,

    /// <summary>
    /// The plain text part, because the mail has no HTML part or nothing visible remains of it.
    /// </summary>
    PLAIN_TEXT,
}