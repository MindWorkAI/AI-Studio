using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// The arguments with which the mail tools narrow down the mails they search or count.
/// </summary>
/// <remarks>
/// Searching and counting take the same conditions, so that a model which counted the unread mails
/// of a sender finds exactly those when it lists them. The schema and the readers share the names
/// of the arguments, and a wrong value is refused with what would have been right, like every other
/// argument, see ToolArgumentReader.
/// </remarks>
internal static class MailToolArguments
{
    public const string MAILBOX_IDS_ARGUMENT = "mailbox_ids";
    public const string FROM_ARGUMENT = "from";
    public const string TO_ARGUMENT = "to";
    public const string AFTER_ARGUMENT = "after";
    public const string BEFORE_ARGUMENT = "before";
    public const string IS_UNREAD_ARGUMENT = "is_unread";
    public const string IS_FLAGGED_ARGUMENT = "is_flagged";
    public const string IS_ENCRYPTED_ARGUMENT = "is_encrypted";
    public const string IMPORTANCE_ARGUMENT = "importance";
    public const string HAS_ATTACHMENTS_ARGUMENT = "has_attachments";
    public const string FOLDER_ARGUMENT = "folder";
    public const string SPECIAL_FOLDER_ARGUMENT = "special_folder";

    public const string SPECIAL_FOLDER_SENT = "sent";
    public const string SPECIAL_FOLDER_DRAFTS = "drafts";

    /// <summary>
    /// How long a part of an address or a name may be. Longer than any address, shorter than a sentence.
    /// </summary>
    private const int MAX_ADDRESS_CHARACTERS = 200;

    /// <summary>
    /// How long the path of a folder may be. Servers allow deep hierarchies, but rarely this deep.
    /// </summary>
    private const int MAX_FOLDER_CHARACTERS = 500;

    private static readonly string[] IMPORTANCE_VALUES = ["low", "normal", "high"];

    private static readonly string[] SPECIAL_FOLDER_VALUES = [SPECIAL_FOLDER_SENT, SPECIAL_FOLDER_DRAFTS];

    /// <summary>
    /// Adds the conditions to the arguments a mail tool describes.
    /// </summary>
    /// <param name="builder">The schema of the tool.</param>
    /// <param name="toolAction">What the tool does with the mails, e.g., "search", completing "the mailboxes to ...".</param>
    /// <param name="mailboxIds">The ids of the mailboxes the tool offers, or none while no mailboxes are known.</param>
    /// <returns>The schema, for further arguments.</returns>
    public static ToolParameterSchemaBuilder AddMailConditions(this ToolParameterSchemaBuilder builder, string toolAction, params string[] mailboxIds) => builder
        .OptionalStringArray(MAILBOX_IDS_ARGUMENT, $"Optional IDs of the mailboxes to {toolAction}, out of those listed in the description of this tool. Leave it out to {toolAction} all of them.", mailboxIds)
        .OptionalString(FROM_ARGUMENT, $"Optional part of the address or the name of the sender, such as 'alice@example.org', 'example.org', or 'Alice'. At most {MAX_ADDRESS_CHARACTERS} characters.")
        .OptionalString(TO_ARGUMENT, $"Optional part of the address or the name of a recipient in To, Cc, or Bcc. At most {MAX_ADDRESS_CHARACTERS} characters.")
        .OptionalString(AFTER_ARGUMENT, "Optional: only mails received at this point in time or later. A date such as 2026-09-01 stands for the beginning of that day in the time zone of the user. A date with a time of day such as 2026-09-01T14:30 is read in that time zone as well, unless it ends with an offset such as +02:00 or with Z.")
        .OptionalString(BEFORE_ARGUMENT, "Optional: only mails received before this point in time, in the same forms as the argument after. A date stands for the beginning of that day, so before 2026-09-30 leaves that day out.")
        .OptionalBoolean(IS_UNREAD_ARGUMENT, "Optional: true for unread mails only, false for read ones only.")
        .OptionalBoolean(IS_FLAGGED_ARGUMENT, "Optional: true for flagged mails only, false for unflagged ones only.")
        .OptionalBoolean(IS_ENCRYPTED_ARGUMENT, "Optional: true for encrypted mails only, false for unencrypted ones only. AI Studio cannot read the content of encrypted mails, only their header.")
        .OptionalEnum(IMPORTANCE_ARGUMENT, "Optional importance the sender marked the mails with. Mails without such a mark count as normal.", IMPORTANCE_VALUES)
        .OptionalBoolean(HAS_ATTACHMENTS_ARGUMENT, "Optional: true for mails with attachments only, false for mails without any.")
        .OptionalString(FOLDER_ARGUMENT, $"Optional full path of the folder the mails lie in, exactly as results show it, such as 'INBOX' or 'Archive/2026'. Subfolders are not included. For the sent mails or the drafts, use {SPECIAL_FOLDER_ARGUMENT} instead.")
        .OptionalEnum(SPECIAL_FOLDER_ARGUMENT, $"Optional: only the mails the user sent, or only the drafts of the user, however the server names these folders. Cannot be combined with {FOLDER_ARGUMENT}.", SPECIAL_FOLDER_VALUES);

    /// <summary>
    /// The value of the importance argument which stands for the given importance.
    /// </summary>
    /// <remarks>
    /// Results name the importance of a mail the same way, so a model can take it over as a condition.
    /// </remarks>
    public static string ToArgumentValue(MailImportance importance) => importance switch
    {
        MailImportance.LOW => "low",
        MailImportance.HIGH => "high",
        _ => "normal",
    };

    /// <summary>
    /// The value of the special folder argument which stands for the given kind of folder.
    /// </summary>
    /// <remarks>
    /// Results mark the mails in such a folder the same way, so a model can take it over as a condition.
    /// </remarks>
    /// <returns>The value, or null for a kind of folder the argument does not offer.</returns>
    public static string? ToArgumentValue(MailFolderSpecialUse specialUse) => specialUse switch
    {
        MailFolderSpecialUse.SENT => SPECIAL_FOLDER_SENT,
        MailFolderSpecialUse.DRAFTS => SPECIAL_FOLDER_DRAFTS,
        _ => null,
    };

    /// <summary>
    /// Reads which of the offered mailboxes the model asked for.
    /// </summary>
    /// <param name="arguments">The arguments the model passed.</param>
    /// <param name="offeredMailboxes">The mailboxes the tool offers, in the order it offers them.</param>
    /// <param name="toolAction">What the tool does with the mails, as for AddMailConditions.</param>
    /// <returns>The mailboxes, in the order they are offered; all of them when the model named none.</returns>
    /// <exception cref="ArgumentException">The model named a mailbox the tool does not offer, with a message for the model to correct it by.</exception>
    public static IReadOnlyList<DataSourceMailbox> ReadMailboxes(JsonElement arguments, IReadOnlyList<DataSourceMailbox> offeredMailboxes, string toolAction)
    {
        var offeredIds = offeredMailboxes.Select(mailbox => mailbox.Id).ToList();
        var requestedIds = ToolArgumentReader.ReadOptionalChoices(arguments, MAILBOX_IDS_ARGUMENT, offeredIds, $"to {toolAction} all listed mailboxes");
        return requestedIds is null
            ? offeredMailboxes
            : offeredMailboxes.Where(mailbox => requestedIds.Contains(mailbox.Id, StringComparer.Ordinal)).ToList();
    }

    /// <summary>
    /// Reads the conditions the mails have to meet.
    /// </summary>
    /// <param name="arguments">The arguments the model passed.</param>
    /// <param name="timeZone">The time zone of the user, in which dates without an offset are read.</param>
    /// <returns>The conditions; without any condition when the model set none.</returns>
    /// <exception cref="ArgumentException">A condition is wrong, with a message for the model to correct it by.</exception>
    public static MailConditions ReadConditions(JsonElement arguments, TimeZoneInfo timeZone)
    {
        var from = ToolArgumentReader.ReadOptionalLine(arguments, FROM_ARGUMENT, MAX_ADDRESS_CHARACTERS, "for mails from any sender");
        var to = ToolArgumentReader.ReadOptionalLine(arguments, TO_ARGUMENT, MAX_ADDRESS_CHARACTERS, "for mails to any recipient");
        var after = ToolArgumentReader.ReadOptionalDateTime(arguments, AFTER_ARGUMENT, timeZone, "for mails of any age");
        var before = ToolArgumentReader.ReadOptionalDateTime(arguments, BEFORE_ARGUMENT, timeZone, "for mails up to now");
        if (after is { } since && before is { } until && since >= until)
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Argument '{AFTER_ARGUMENT}' must lie before argument '{BEFORE_ARGUMENT}', but no mail can arrive at {since:yyyy-MM-dd'T'HH:mmzzz} or later and before {until:yyyy-MM-dd'T'HH:mmzzz}. Swap the two, or leave one of them out."));

        var importance = ToolArgumentReader.ReadOptionalChoice(arguments, IMPORTANCE_ARGUMENT, IMPORTANCE_VALUES, "for mails of any importance") switch
        {
            null => (MailImportance?)null,
            "low" => MailImportance.LOW,
            "normal" => MailImportance.NORMAL,
            "high" => MailImportance.HIGH,
            var other => throw new UnreachableException($"The importance '{other}' was offered, but has no meaning."),
        };

        var filter = new MailFilter
        {
            From = from,
            To = to,
            ReceivedSinceUtc = after?.ToUniversalTime(),
            ReceivedBeforeUtc = before?.ToUniversalTime(),
            IsUnread = ToolArgumentReader.ReadOptionalBoolean(arguments, IS_UNREAD_ARGUMENT, "for read and unread mails alike"),
            IsFlagged = ToolArgumentReader.ReadOptionalBoolean(arguments, IS_FLAGGED_ARGUMENT, "for flagged and unflagged mails alike"),
            IsEncrypted = ToolArgumentReader.ReadOptionalBoolean(arguments, IS_ENCRYPTED_ARGUMENT, "for encrypted and unencrypted mails alike"),
            Importance = importance,
            HasAttachments = ToolArgumentReader.ReadOptionalBoolean(arguments, HAS_ATTACHMENTS_ARGUMENT, "for mails with and without attachments alike"),
        };

        var folder = ToolArgumentReader.ReadOptionalLine(arguments, FOLDER_ARGUMENT, MAX_FOLDER_CHARACTERS, "for mails in any folder");
        var specialFolder = ToolArgumentReader.ReadOptionalChoice(arguments, SPECIAL_FOLDER_ARGUMENT, SPECIAL_FOLDER_VALUES, "for mails in any folder") switch
        {
            null => (MailFolderSpecialUse?)null,
            SPECIAL_FOLDER_SENT => MailFolderSpecialUse.SENT,
            SPECIAL_FOLDER_DRAFTS => MailFolderSpecialUse.DRAFTS,
            var other => throw new UnreachableException($"The special folder '{other}' was offered, but has no meaning."),
        };

        // Both name the one folder the mails have to lie in, and two such folders would match no mail at all:
        if (folder is not null && specialFolder is not null)
            throw new ArgumentException($"Arguments '{FOLDER_ARGUMENT}' and '{SPECIAL_FOLDER_ARGUMENT}' cannot be combined, but both were given. Leave out '{FOLDER_ARGUMENT}' for the sent mails or the drafts, however the server names their folders, or leave out '{SPECIAL_FOLDER_ARGUMENT}' for the folder with the given path.");

        return new(filter, folder, specialFolder);
    }
}