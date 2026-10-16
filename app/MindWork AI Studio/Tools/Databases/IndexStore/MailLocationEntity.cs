namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// One place on the server where a mail lies.
/// </summary>
/// <remarks>
/// A mail can lie in more than one folder, and moving it changes only its locations, never its
/// embedding. The flags are kept here, not with the mail, because IMAP keeps them per folder: a
/// copy in another folder has flags of its own.
/// </remarks>
internal sealed class MailLocationEntity
{
    public int Id { get; set; }

    public string ParentFileId { get; set; } = string.Empty;

    public int FolderId { get; set; }

    /// <summary>
    /// The UID of the mail within its folder, valid for the UIDVALIDITY of that folder.
    /// </summary>
    public long Uid { get; set; }

    public bool IsSeen { get; set; }

    public bool IsFlagged { get; set; }

    public bool IsAnswered { get; set; }

    public MailMessageEntity? Message { get; set; }

    public MailFolderEntity? Folder { get; set; }
}