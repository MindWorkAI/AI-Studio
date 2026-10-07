namespace AIStudio.Tools.Mail;

/// <summary>
/// The folders of a mailbox the sync works through.
/// </summary>
/// <param name="Folders">The folders, the inbox first and the others by their full names.</param>
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
    /// The virtual folders for flagged and for important mails count only when they are the root
    /// folder. Each of them shows mails which lie in another folder as well, so the sync would only
    /// fetch the same mails twice. The folder holding all mails is different: at Gmail, an archived
    /// mail lies there and nowhere else.
    /// </remarks>
    /// <param name="serverFolders">The folders as the server lists them.</param>
    /// <param name="rootFolder">The full name of the root folder, or empty for the whole mailbox.</param>
    /// <returns>The selection.</returns>
    public static MailFolderSelection Select(IReadOnlyList<MailServerFolder> serverFolders, string rootFolder)
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
            .Where(folder => !hasRootFolder || IsAtOrBelow(folder, rootFolder))
            .Where(folder => folder.SpecialUse is not (MailFolderSpecialUse.FLAGGED or MailFolderSpecialUse.IMPORTANT) || (hasRootFolder && IsRootFolder(folder, rootFolder)))
            .OrderByDescending(folder => folder.IsInbox)
            .ThenBy(folder => folder.FullName, StringComparer.Ordinal)
            .ToList();

        var rootFolderFound = !hasRootFolder || serverFolders.Any(folder => IsRootFolder(folder, rootFolder));
        return new(folders, rootFolderFound);
    }

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