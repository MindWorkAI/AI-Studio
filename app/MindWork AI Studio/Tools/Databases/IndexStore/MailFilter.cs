using AIStudio.Tools.Mail;

namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// Conditions a mail has to meet. A condition left out holds for every mail.
/// </summary>
/// <remarks>
/// A mail counts as read, flagged or answered when it is so in any of its folders: whoever read
/// the copy in the inbox read the mail, even when another copy in an archive still says unread.
/// Mails which lost their last place on the server never match, since they are gone or about to
/// show up somewhere else.
/// </remarks>
public sealed record MailFilter
{
    /// <summary>
    /// A part of the address or the name of the sender, as the From or Sender header gives it.
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// A part of the address or the name of a recipient, as the To, Cc or Bcc header gives it.
    /// </summary>
    public string? To { get; init; }

    /// <summary>
    /// Only mails which arrived at the server at this point in time or later.
    /// </summary>
    public DateTimeOffset? ReceivedSinceUtc { get; init; }

    /// <summary>
    /// Only mails which arrived at the server before this point in time.
    /// </summary>
    public DateTimeOffset? ReceivedBeforeUtc { get; init; }

    public bool? IsUnread { get; init; }

    public bool? IsFlagged { get; init; }

    public bool? IsEncrypted { get; init; }

    public MailImportance? Importance { get; init; }

    public bool? HasAttachments { get; init; }

    /// <summary>
    /// Only mails which lie in one of these folders, given by their full paths.
    /// </summary>
    /// <remarks>
    /// An empty collection matches no mail at all. It never turns into "any folder": a caller
    /// whose folder names matched no folder must not get the whole mailbox back.
    /// </remarks>
    public IReadOnlyCollection<string>? FolderPaths { get; init; }

    /// <summary>
    /// Whether any condition is set at all.
    /// </summary>
    public bool HasConditions =>
        !string.IsNullOrWhiteSpace(this.From)
        || !string.IsNullOrWhiteSpace(this.To)
        || this.ReceivedSinceUtc is not null
        || this.ReceivedBeforeUtc is not null
        || this.IsUnread is not null
        || this.IsFlagged is not null
        || this.IsEncrypted is not null
        || this.Importance is not null
        || this.HasAttachments is not null
        || this.FolderPaths is not null;
}