using System.Globalization;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// How the mail tools show mails to the model, and what a chat has to keep once it saw them.
/// </summary>
/// <remarks>
/// Searching, reading, and counting show a mail the same way, so that the model recognizes it in
/// every result and can take a value of one over into the arguments of another.
/// </remarks>
internal static class MailToolResults
{
    public const string NO_SUBJECT = "(no subject)";
    public const string UNKNOWN_SENDER = "(unknown sender)";

    /// <summary>
    /// A point in time as the user would read it, in their time zone and with its offset.
    /// </summary>
    public static string FormatTime(DateTimeOffset pointInTime, TimeZoneInfo timeZone) => TimeZoneInfo.ConvertTime(pointInTime, timeZone).ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);

    public static string FormatAddress(MailAddressRecord address) => string.IsNullOrWhiteSpace(address.DisplayName) ? address.Address : $"{address.DisplayName} <{address.Address}>";

    /// <summary>
    /// Who sent a mail: the From header, or the Sender header when there is no From.
    /// </summary>
    public static MailAddressRecord? FindSender(IReadOnlyList<MailAddressRecord> addresses) =>
        addresses.FirstOrDefault(address => address.Role is MailAddressRole.FROM) ?? addresses.FirstOrDefault(address => address.Role is MailAddressRole.SENDER);

    /// <summary>
    /// How to name the sender in a short line: by name when the mail gives one, otherwise by address.
    /// </summary>
    public static string GetSenderName(MailAddressRecord? sender) => sender switch
    {
        null => UNKNOWN_SENDER,
        { DisplayName: var displayName } when !string.IsNullOrWhiteSpace(displayName) => displayName,
        _ => sender.Address,
    };

    public static string GetEncryptionName(MailEncryptionKind encryptionKind) => encryptionKind switch
    {
        MailEncryptionKind.SMIME => "S/MIME",
        MailEncryptionKind.SMIME_OPAQUE_SIGNED => "S/MIME, signed opaquely",
        MailEncryptionKind.PGP_MIME => "PGP/MIME",
        MailEncryptionKind.PGP_INLINE => "inline PGP",
        MailEncryptionKind.MICROSOFT_IRM => "Microsoft rights management",
        _ => "unknown",
    };

    /// <summary>
    /// The source a mail which reached the model leaves in the chat.
    /// </summary>
    /// <remarks>
    /// The address is unique per mail, because the sources of a chat are told apart by it. It
    /// leads nowhere yet; the mail viewer will open it. Subject and sender have to be filtered
    /// already, since the user reads the title.
    /// </remarks>
    /// <param name="mailbox">The mailbox the mail belongs to.</param>
    /// <param name="mailId">The id of the mail.</param>
    /// <param name="subject">The subject, filtered for prompt injections.</param>
    /// <param name="senderName">The name of the sender, filtered as well.</param>
    /// <param name="receivedAtUtc">When the mail arrived at the server.</param>
    /// <param name="timeZone">The time zone of the user.</param>
    public static Source CreateSource(DataSourceMailbox mailbox, string mailId, string subject, string senderName, DateTimeOffset receivedAtUtc, TimeZoneInfo timeZone) => new(
        string.Create(CultureInfo.InvariantCulture, $"Mail: {subject} — {senderName}, {TimeZoneInfo.ConvertTime(receivedAtUtc, timeZone):yyyy-MM-dd}"),
        $"mailbox://{mailbox.Id}/{mailId}",
        SourceOrigin.TOOL);

    /// <summary>
    /// What the chat has to require from now on, because of the mailboxes whose content reached the model.
    /// </summary>
    /// <remarks>
    /// A result which brought nothing of a mailbox into the chat requires nothing for it. Of several
    /// mailboxes, the strictest restriction wins; on a tie, the first one is named, so the chat does
    /// not name another mailbox with every search.
    /// </remarks>
    /// <param name="contributingMailboxes">The mailboxes whose content reached the model.</param>
    /// <returns>The provider confidence and the outbound data restriction the chat requires from now on.</returns>
    public static (ConfidenceLevel Confidence, OutboundDataRequirement OutboundData) GetRequirements(IEnumerable<DataSourceMailbox> contributingMailboxes)
    {
        var confidence = ConfidenceLevel.NONE;
        var outboundData = OutboundDataRequirement.NONE;
        foreach (var mailbox in contributingMailboxes)
        {
            if (mailbox.ConfidenceLevel > confidence)
                confidence = mailbox.ConfidenceLevel;

            outboundData = outboundData.StricterOf(new(mailbox.OutboundDataRestriction, mailbox.Id));
        }

        return (confidence, outboundData);
    }
}