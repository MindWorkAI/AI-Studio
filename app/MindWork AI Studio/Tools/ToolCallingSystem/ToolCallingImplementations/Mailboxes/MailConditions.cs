using AIStudio.Tools.Databases.IndexStore;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// The conditions a model set for the mails a tool searches or counts.
/// </summary>
/// <remarks>
/// The folder stays a name until a mailbox is searched: every mailbox has folders of its own, and
/// only its own list can tell which of them the name means.
/// </remarks>
/// <param name="Filter">The conditions apart from the folder.</param>
/// <param name="Folder">The full path of the folder the mails have to lie in, as the model wrote it, or null for any folder.</param>
internal sealed record MailConditions(MailFilter Filter, string? Folder)
{
    /// <summary>
    /// The conditions for one mailbox, with the folder turned into the paths it stands for there.
    /// </summary>
    /// <remarks>
    /// A folder is found by its full path, regardless of case, since a model writes "Inbox" as
    /// readily as "INBOX". Its subfolders are not included: they are folders of their own, and the
    /// model can name them. In a mailbox without such a folder, the condition matches no mail at
    /// all, never every mail.
    /// </remarks>
    /// <param name="folders">The folders of the mailbox.</param>
    /// <returns>The conditions for that mailbox.</returns>
    public MailFilter ForMailbox(IReadOnlyList<MailFolderRecord> folders) => this.Folder is null
        ? this.Filter
        : this.Filter with { FolderPaths = folders.Where(folder => string.Equals(folder.Path, this.Folder, StringComparison.OrdinalIgnoreCase)).Select(folder => folder.Path).ToList() };
}