namespace AIStudio.Tools.Mail;

/// <summary>
/// The folders of a mailbox the sync works through.
/// </summary>
/// <param name="Folders">The folders: the inbox first, then those for sent mails and for drafts, then the others by their full names.</param>
/// <param name="RootFolderFound">False when the mailbox is limited to a root folder the server no longer lists, e.g. because somebody renamed it.</param>
public sealed record MailFolderSelection(IReadOnlyList<MailServerFolder> Folders, bool RootFolderFound)
{
    /// <summary>
    /// Picks the folders of a mailbox the sync works through.
    /// </summary>
    /// <remarks>
    /// Every folder which can hold mails, at or below the root folder when the mailbox has one. The
    /// trash and the junk folder never count: what lies there was thrown away or never wanted, and
    /// the root folder cannot be one of them either.
    ///
    /// The folders for sent mails and for drafts may come along from outside the root folder. Only
    /// the folders themselves, as the server marks them, without their subfolders. Their names
    /// differ by server and language, so a server which does not mark them leaves nothing to tell
    /// them by, and guessing by name could pull in any folder.
    ///
    /// The virtual folders for flagged and for important mails count only when they are the root
    /// folder. Each of them shows mails which lie in another folder as well, so the sync would only
    /// fetch the same mails twice. The folder holding all mails is different: at Gmail, an archived
    /// mail lies there and nowhere else.
    ///
    /// The sent mails and the drafts follow right after the inbox, so that the first sync of a
    /// mailbox reaches them early, before the folders the user sorted mails into.
    /// </remarks>
    /// <param name="serverFolders">The folders as the server lists them.</param>
    /// <param name="rootFolder">The full name of the root folder, or empty for the whole mailbox.</param>
    /// <param name="includeSentAndDrafts">Whether the folders for sent mails and for drafts count outside the root folder as well.</param>
    /// <returns>The selection.</returns>
    public static MailFolderSelection Select(IReadOnlyList<MailServerFolder> serverFolders, string rootFolder, bool includeSentAndDrafts)
    {
        //
        // Empty, not blank, stands for the whole mailbox: the empty name is what the folder picker
        // stores for it. A name of nothing but spaces is a folder name like any other. When the server
        // does not list such a folder, the root folder counts as missing and the sync stops. Reading
        // it as the whole mailbox instead would quietly widen the sync to everything, at the cost of
        // embedding all of it.
        //
        var hasRootFolder = !string.IsNullOrEmpty(rootFolder);
        var folders = serverFolders
            .Where(folder => folder is { CanSelect: true, SpecialUse: not (MailFolderSpecialUse.TRASH or MailFolderSpecialUse.JUNK) })
            .Where(folder => !hasRootFolder || IsAtOrBelow(folder, rootFolder) || (includeSentAndDrafts && folder.SpecialUse is (MailFolderSpecialUse.SENT or MailFolderSpecialUse.DRAFTS)))
            .Where(folder => folder.SpecialUse is not (MailFolderSpecialUse.FLAGGED or MailFolderSpecialUse.IMPORTANT) || (hasRootFolder && IsRootFolder(folder, rootFolder)))
            .OrderBy(GetSyncRank)
            .ThenBy(folder => folder.FullName, StringComparer.Ordinal)
            .ToList();

        var rootFolderFound = !hasRootFolder || serverFolders.Any(folder => IsRootFolder(folder, rootFolder));
        return new(folders, rootFolderFound);
    }

    /// <summary>
    /// From which day on the mails of a folder belong into the index.
    /// </summary>
    /// <remarks>
    /// Drafts count however old they are, as flagged mails do: a draft was never received, and an
    /// old one may be just the one the user wants to finish now.
    /// </remarks>
    /// <param name="folder">The folder.</param>
    /// <param name="receivedSince">The first day of the period of the mailbox, or null for all mails.</param>
    /// <returns>The first day for this folder, or null for all of its mails.</returns>
    public static DateTimeOffset? GetReceivedSince(MailServerFolder folder, DateTimeOffset? receivedSince) => folder.SpecialUse is MailFolderSpecialUse.DRAFTS ? null : receivedSince;

    /// <summary>
    /// Where a folder comes in the order the sync works through them.
    /// </summary>
    private static int GetSyncRank(MailServerFolder folder) => folder switch
    {
        { IsInbox: true } => 0,
        { SpecialUse: MailFolderSpecialUse.SENT } => 1,
        { SpecialUse: MailFolderSpecialUse.DRAFTS } => 2,
        _ => 3,
    };

    /// <summary>
    /// Whether a folder is the root folder, which IMAP names case-insensitively only for the inbox.
    /// </summary>
    private static bool IsRootFolder(MailServerFolder folder, string rootFolder) =>
        folder.FullName.Equals(rootFolder, folder.IsInbox ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsAtOrBelow(MailServerFolder folder, string rootFolder)
    {
        if (IsRootFolder(folder, rootFolder))
            return true;

        // A server without a hierarchy has no folders below another one:
        if (folder.DirectorySeparator is '\0')
            return false;

        //
        // Only the inbox is matched case-insensitively, and folders below it inherit that, since
        // the server lists them as "INBOX/…" whatever the user typed:
        //
        var comparison = rootFolder.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return folder.FullName.StartsWith(rootFolder + folder.DirectorySeparator, comparison);
    }
}