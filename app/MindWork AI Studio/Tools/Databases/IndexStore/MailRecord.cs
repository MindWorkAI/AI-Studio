using AIStudio.Tools.Mail;

namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// What the index keeps about one mail beyond its chunks.
/// </summary>
/// <remarks>
/// The addresses come back grouped by their role and the parts by their kind, each group in the
/// order it was stored in. The locations come back ordered by folder and UID.
/// </remarks>
/// <param name="MailId">The id of the mail, which is the id of its document.</param>
/// <param name="MessageId">The Message-ID header, empty when the mail has none.</param>
/// <param name="InReplyTo">The Message-ID the In-Reply-To header names, empty when there is none.</param>
/// <param name="ReferenceMessageIds">The Message-IDs of the References header, oldest first.</param>
/// <param name="SentAtUtc">When the sender says the mail was written, or null when it does not say so readably.</param>
/// <param name="ReceivedAtUtc">When the mail arrived at the server.</param>
/// <param name="Importance">How important the sender marked the mail.</param>
/// <param name="EncryptionKind">How the content of the mail is encrypted, NONE when it is not.</param>
/// <param name="MailHash">A hash over the mail as the server delivered it.</param>
/// <param name="FirstSeenUtc">When AI Studio found the mail for the first time.</param>
/// <param name="Addresses">The addresses from the header of the mail.</param>
/// <param name="Parts">The parts of the mail as AI Studio read them.</param>
/// <param name="Locations">Where the mail lies on the server, at least one place.</param>
public sealed record MailRecord(
    string MailId,
    string MessageId,
    string InReplyTo,
    IReadOnlyList<string> ReferenceMessageIds,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset ReceivedAtUtc,
    MailImportance Importance,
    MailEncryptionKind EncryptionKind,
    string MailHash,
    DateTimeOffset FirstSeenUtc,
    IReadOnlyList<MailAddressRecord> Addresses,
    IReadOnlyList<MailPartRecord> Parts,
    IReadOnlyList<MailLocationRecord> Locations);