namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// How to break down the number of mails in a mailbox.
/// </summary>
public enum MailCountGrouping
{
    /// <summary>
    /// Only the total.
    /// </summary>
    NONE,

    /// <summary>
    /// By the folders the mails lie in. A mail in two folders counts in both.
    /// </summary>
    FOLDER,

    /// <summary>
    /// By the address in the From header.
    /// </summary>
    SENDER,
}