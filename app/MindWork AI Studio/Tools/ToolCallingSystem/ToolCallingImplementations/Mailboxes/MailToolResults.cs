using System.Globalization;
using System.Text.Json.Nodes;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services;

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
    /// How many folders a result lists when the folder the model asked for does not exist.
    /// </summary>
    public const int MAX_LISTED_FOLDERS = 50;

    /// <summary>
    /// The conditions as the tool read them, so the model sees how its dates were understood.
    /// </summary>
    /// <remarks>
    /// The conditions came from the model, not from a mail, so they need no filtering.
    /// </remarks>
    public static JsonObject DescribeConditions(MailConditions conditions, TimeZoneInfo timeZone)
    {
        var filter = conditions.Filter;
        var description = new JsonObject();
        if (filter.From is { } from)
            description[MailToolArguments.FROM_ARGUMENT] = from;

        if (filter.To is { } to)
            description[MailToolArguments.TO_ARGUMENT] = to;

        if (filter.ReceivedSinceUtc is { } receivedSince)
            description["received_at_or_after"] = FormatTime(receivedSince, timeZone);

        if (filter.ReceivedBeforeUtc is { } receivedBefore)
            description["received_before"] = FormatTime(receivedBefore, timeZone);

        if (filter.IsUnread is { } isUnread)
            description[MailToolArguments.IS_UNREAD_ARGUMENT] = isUnread;

        if (filter.IsFlagged is { } isFlagged)
            description[MailToolArguments.IS_FLAGGED_ARGUMENT] = isFlagged;

        if (filter.IsEncrypted is { } isEncrypted)
            description[MailToolArguments.IS_ENCRYPTED_ARGUMENT] = isEncrypted;

        if (filter.Importance is { } importance)
            description[MailToolArguments.IMPORTANCE_ARGUMENT] = MailToolArguments.ToArgumentValue(importance);

        if (filter.HasAttachments is { } hasAttachments)
            description[MailToolArguments.HAS_ATTACHMENTS_ARGUMENT] = hasAttachments;

        if (conditions.Folder is { } folder)
            description[MailToolArguments.FOLDER_ARGUMENT] = folder;

        return description;
    }

    /// <summary>
    /// Adds how far the index of a mailbox reaches to what the model learns about it, and what the index does not cover to its issues.
    /// </summary>
    /// <remarks>
    /// Only AI Studio's own values: points in time, counts, and sentences of its own.
    /// </remarks>
    /// <param name="description">What the model learns about the mailbox.</param>
    /// <param name="issues">What kept the result from covering the whole mailbox.</param>
    /// <param name="coverage">How far the index reaches, or null when that cannot be read.</param>
    /// <param name="timeZone">The time zone of the user.</param>
    public static void DescribeCoverage(JsonObject description, JsonArray issues, MailboxCoverage? coverage, TimeZoneInfo timeZone)
    {
        if (coverage is null)
        {
            issues.Add("How far the index of this mailbox reaches cannot be told right now.");
            return;
        }

        if (coverage.ReceivedSinceUtc is { } receivedSince)
            description["indexed_since"] = FormatTime(receivedSince, timeZone);
        else
            description["indexes_all_mails"] = true;

        if (coverage.LastCompleteSyncUtc is { } lastSync)
            description["last_complete_sync"] = FormatTime(lastSync, timeZone);
        else
            issues.Add("The first sync of this mailbox is still running, so some of its mails are not in the index yet.");

        if (coverage.SignInRefusedAtUtc is { } refusedAt)
            issues.Add($"The server of this mailbox refused to let AI Studio sign in at {FormatTime(refusedAt, timeZone)}. Mails which arrived since then are missing until the user enters the current password in the settings of the mailbox.");

        if (coverage.PendingRemovalCount is { } pendingRemovalCount)
            issues.Add($"The index still holds {pendingRemovalCount} mails which a sync would have removed, because they are no longer on the server or no longer within the folders and the period of this mailbox. Some of the mails found or counted may be among them.");
    }

    /// <summary>
    /// Registers the folders of a mailbox to be listed, because the folder the model asked for does not exist there.
    /// </summary>
    /// <returns>The indices of the folder paths in the texts, at most MAX_LISTED_FOLDERS of them.</returns>
    public static IReadOnlyList<int> RegisterFolderList(MailboxCoverage coverage, DataSourceMailbox mailbox, MailTexts texts) =>
        coverage.Folders.Take(MAX_LISTED_FOLDERS).Select(folder => texts.Add(folder.Path, mailbox)).ToList();

    /// <summary>
    /// Lists the folders of a mailbox which has no folder with the path the model gave, so the model can pick one.
    /// </summary>
    /// <param name="description">What the model learns about the mailbox.</param>
    /// <param name="issues">What kept the result from covering the whole mailbox.</param>
    /// <param name="listedFolders">The folder paths to list, filtered for prompt injections.</param>
    /// <param name="folderCount">How many folders the mailbox has.</param>
    public static void DescribeMissingFolder(JsonObject description, JsonArray issues, IReadOnlyList<string> listedFolders, int folderCount)
    {
        issues.Add(folderCount > listedFolders.Count
            ? $"This mailbox has no folder with the path given in '{MailToolArguments.FOLDER_ARGUMENT}'. The first {listedFolders.Count} of its {folderCount} folders are listed in 'folders'."
            : $"This mailbox has no folder with the path given in '{MailToolArguments.FOLDER_ARGUMENT}'. Its folders are listed in 'folders'.");

        description["folders"] = new JsonArray([..listedFolders.Select(folder => (JsonNode?)folder)]);
    }

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
    /// The address leads nowhere yet, so the list of sources shows the title as text; the mail
    /// viewer will open it. Subject and sender have to be filtered already, since the user reads
    /// the title.
    /// </remarks>
    /// <param name="mailbox">The mailbox the mail belongs to.</param>
    /// <param name="mailId">The id of the mail.</param>
    /// <param name="subject">The subject, filtered for prompt injections.</param>
    /// <param name="senderName">The name of the sender, filtered as well.</param>
    /// <param name="receivedAtUtc">When the mail arrived at the server.</param>
    /// <param name="timeZone">The time zone of the user.</param>
    public static Source CreateSource(DataSourceMailbox mailbox, string mailId, string subject, string senderName, DateTimeOffset receivedAtUtc, TimeZoneInfo timeZone) => new(
        string.Create(CultureInfo.InvariantCulture, $"Mail: {subject} — {senderName}, {TimeZoneInfo.ConvertTime(receivedAtUtc, timeZone):yyyy-MM-dd}"),
        SourceExtensions.CreateMailSourceUrl(mailbox.Id, mailId),
        SourceOrigin.TOOL);

    /// <summary>
    /// What the chat has to require from now on, because of the mailboxes whose content reached the model.
    /// </summary>
    /// <remarks>
    /// A result which brought nothing of a mailbox into the chat requires nothing for it. Of several
    /// mailboxes, the strictest restriction wins; on a tie, the first one is named, so the chat does
    /// not name another mailbox with every search. A mailbox set to a less strict restriction than
    /// the organization allows counts with the least strict one allowed.
    /// </remarks>
    /// <param name="contributingMailboxes">The mailboxes whose content reached the model.</param>
    /// <param name="minimumOutboundDataRestriction">The least strict restriction the organization allows, see DataMailboxes.MinimumOutboundDataRestriction.</param>
    /// <returns>The provider confidence and the outbound data restriction the chat requires from now on.</returns>
    public static (ConfidenceLevel Confidence, OutboundDataRequirement OutboundData) GetRequirements(IEnumerable<DataSourceMailbox> contributingMailboxes, OutboundDataRestriction minimumOutboundDataRestriction)
    {
        var confidence = ConfidenceLevel.NONE;
        var outboundData = OutboundDataRequirement.NONE;
        foreach (var mailbox in contributingMailboxes)
        {
            if (mailbox.ConfidenceLevel > confidence)
                confidence = mailbox.ConfidenceLevel;

            outboundData = outboundData.StricterOf(new(mailbox.OutboundDataRestriction.StricterOf(minimumOutboundDataRestriction), mailbox.Id));
        }

        return (confidence, outboundData);
    }
}