namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// What the index knows about one mail beyond its text, one row per mail and mailbox.
/// </summary>
/// <remarks>
/// The mail itself is a row in embedded_files, whose primary key this row shares. Indexing a mail
/// again deletes that row first, and the cascade takes this one with it. So this row is written
/// after the last chunk of a mail, never before.
/// </remarks>
internal sealed class MailMessageEntity
{
    public string ParentFileId { get; set; } = string.Empty;

    public string DataSourceId { get; set; } = string.Empty;

    /// <summary>
    /// The Message-ID header, empty when the mail has none.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// The Message-ID the In-Reply-To header names, empty when there is none.
    /// </summary>
    public string InReplyTo { get; set; } = string.Empty;

    /// <summary>
    /// The Message-IDs of the References header, oldest first and separated by spaces, the way the
    /// header itself lists them. A Message-ID cannot contain a space, so nothing gets lost.
    /// </summary>
    public string ReferenceMessageIds { get; set; } = string.Empty;

    /// <summary>
    /// When the sender says the mail was written (Date header), or null when it does not say so readably.
    /// </summary>
    public DateTimeOffset? SentAtUtc { get; set; }

    /// <summary>
    /// When the mail arrived at the server (INTERNALDATE).
    /// </summary>
    /// <remarks>
    /// Unlike the Date header, the sender has no say in this one. It is also the date IMAP compares
    /// against when the time range of a mailbox gets searched.
    /// </remarks>
    public DateTimeOffset ReceivedAtUtc { get; set; }

    /// <summary>
    /// LOW, NORMAL or HIGH, stored by name.
    /// </summary>
    public string Importance { get; set; } = string.Empty;

    /// <summary>
    /// How the content of the mail is encrypted, NONE when it is not, stored by name.
    /// </summary>
    /// <remarks>
    /// There is no separate flag for whether a mail is encrypted: it would only be a second copy of
    /// this column, free to disagree with it. A kind written by a newer version still is not NONE,
    /// so such a mail still counts as encrypted.
    /// </remarks>
    public string EncryptionKind { get; set; } = string.Empty;

    /// <summary>
    /// A hash over the mail as the server delivered it, independent of how AI Studio turns it into text.
    /// </summary>
    public string MailHash { get; set; } = string.Empty;

    /// <summary>
    /// When AI Studio found this mail for the first time.
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; set; }

    /// <summary>
    /// When the mail lost its last location, or null while it has one.
    /// </summary>
    /// <remarks>
    /// A mail which is moved disappears from one folder before it shows up in the other one, maybe
    /// only in the next run. Removing it at once would embed it again when it reappears, so it stays
    /// for one more run.
    /// </remarks>
    public DateTimeOffset? OrphanedAtUtc { get; set; }

    public List<MailAddressEntity> Addresses { get; set; } = [];

    public List<MailPartEntity> Parts { get; set; } = [];

    public List<MailLocationEntity> Locations { get; set; } = [];
}