using System.Text.Json.Nodes;

using AIStudio.Chat;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// A mail found, with its texts waiting to be filtered.
/// </summary>
/// <remarks>
/// <para>
/// Whether the mail lies among the sent mails or the drafts is AI Studio's own value, so it needs no filtering.
/// </para>
/// <para>
/// A result shows only a part of a mail: every text has a limit, and so has every list. Together,
/// they keep a single mail far below a page of the search, see SearchMailsTool.MAX_RESULT_CHARACTERS,
/// even when every character has to be escaped in JSON. Otherwise, a mail with hundreds of
/// attachments would fill a page on its own. read_mail shows the whole mail.
/// </para>
/// </remarks>
internal sealed record PendingMail(DataSourceMailbox Mailbox, MailSummary Summary, int Subject, int From, int Sender, IReadOnlyList<int> Recipients, int MoreRecipients, IReadOnlyList<int> Folders, int MoreFolders, string? SpecialFolder, IReadOnlyList<int> Attachments, int MoreAttachments, int? Passage, ChunkLocation? PassageLocation, int? PassageAttachment)
{
    /// <summary>
    /// How much of the passage which matched best a result shows.
    /// </summary>
    /// <remarks>
    /// Enough to tell whether a mail answers the question. A chunk can be tens of thousands of
    /// characters long, and a page lists several mails per mailbox, so a whole chunk each would use
    /// up the budget after a few mails.
    /// </remarks>
    private const int MAX_PASSAGE_CHARACTERS = 1_500;

    /// <summary>
    /// How much of a subject a result shows. Subjects are short, unless somebody wrote a letter into one.
    /// </summary>
    private const int MAX_SUBJECT_CHARACTERS = 300;

    /// <summary>
    /// How many recipients of a mail a result names. A mail to a large list would otherwise fill the result.
    /// </summary>
    private const int MAX_LISTED_RECIPIENTS = 5;

    /// <summary>
    /// How many folders of a mail a result names. A mail lies in one folder, as a rule, but a server
    /// which shows labels as folders may put it into many.
    /// </summary>
    private const int MAX_LISTED_FOLDERS = 10;

    /// <summary>
    /// How many attachments of a mail a result names, as many as the index lists in the text of a mail, cf. MailTextBuilder.
    /// </summary>
    private const int MAX_LISTED_ATTACHMENTS = 20;

    /// <summary>
    /// How much of an address, of the name of a sender, of a folder, or of the name of an attachment a result shows.
    /// </summary>
    /// <remarks>
    /// As much as of a subject. Nothing keeps a sender from writing a display name or a file name of any length.
    /// </remarks>
    private const int MAX_NAME_CHARACTERS = MAX_SUBJECT_CHARACTERS;

    /// <summary>
    /// Registers everything a result shows of a mail with the texts to filter.
    /// </summary>
    /// <remarks>
    /// The attachment the passage is from is named even when the list of attachments ends before
    /// it. Otherwise, the model would take the passage for one of the text of the mail.
    /// </remarks>
    /// <param name="hit">The mail found.</param>
    /// <param name="mailbox">The mailbox it lies in.</param>
    /// <param name="mailboxFolders">The folders of the mailbox, to tell the sent mails and the drafts.</param>
    /// <param name="texts">The texts of the result, to be filtered together.</param>
    /// <returns>The mail, waiting for its texts to be filtered.</returns>
    public static PendingMail Register(MailSearchHit hit, DataSourceMailbox mailbox, IReadOnlyList<MailFolderRecord> mailboxFolders, MailTexts texts)
    {
        var summary = hit.Summary;
        var sender = MailToolResults.FindSender(summary.Addresses);
        var recipients = summary.Addresses.Where(address => address.Role is MailAddressRole.TO or MailAddressRole.CC).ToList();
        var attachments = summary.AttachmentNames.Take(MAX_LISTED_ATTACHMENTS).Select(name => texts.Add(name.Shorten(MAX_NAME_CHARACTERS), mailbox)).ToList();

        int? passageAttachment = null;
        if (hit is { Passage: not null, PassageLocation.AttachmentPosition: { } position } && position >= 0 && position < summary.AttachmentNames.Count)
            passageAttachment = position < attachments.Count ? attachments[position] : texts.Add(summary.AttachmentNames[position].Shorten(MAX_NAME_CHARACTERS), mailbox);

        return new(
            mailbox,
            summary,
            texts.Add(string.IsNullOrWhiteSpace(summary.Subject) ? MailToolResults.NO_SUBJECT : summary.Subject.Shorten(MAX_SUBJECT_CHARACTERS), mailbox),
            texts.Add(sender is null ? MailToolResults.UNKNOWN_SENDER : MailToolResults.FormatAddress(sender).Shorten(MAX_NAME_CHARACTERS), mailbox),
            texts.Add(MailToolResults.GetSenderName(sender).Shorten(MAX_NAME_CHARACTERS), mailbox),
            recipients.Take(MAX_LISTED_RECIPIENTS).Select(recipient => texts.Add(MailToolResults.FormatAddress(recipient).Shorten(MAX_NAME_CHARACTERS), mailbox)).ToList(),
            Math.Max(0, recipients.Count - MAX_LISTED_RECIPIENTS),
            summary.FolderPaths.Take(MAX_LISTED_FOLDERS).Select(folder => texts.Add(folder.Shorten(MAX_NAME_CHARACTERS), mailbox)).ToList(),
            Math.Max(0, summary.FolderPaths.Count - MAX_LISTED_FOLDERS),
            MailToolResults.GetSpecialFolder(summary.FolderPaths, mailboxFolders),
            attachments,
            Math.Max(0, summary.AttachmentNames.Count - MAX_LISTED_ATTACHMENTS),
            hit.Passage is null ? null : texts.Add(hit.Passage.Shorten(MAX_PASSAGE_CHARACTERS), mailbox),
            hit.PassageLocation,
            passageAttachment);
    }

    /// <summary>
    /// Writes what the model and the user get of this mail, once its texts went through the filter.
    /// </summary>
    /// <remarks>
    /// The mail is measured as the model receives it, so that a search can tell whether it fits.
    /// Mails are listed without a link, so what the model received of one is all there is to read
    /// under it.
    /// </remarks>
    /// <param name="texts">The filtered texts of the result.</param>
    /// <param name="timeZone">The time zone of the user.</param>
    /// <returns>The mail, written for the model and for the user.</returns>
    public DescribedMail Describe(MailTexts texts, TimeZoneInfo timeZone)
    {
        var description = this.DescribeForModel(texts, timeZone);
        var source = this.ToSource(texts, timeZone);
        return new(description, source, this.ToPassage(source, texts, timeZone), description.ToJsonString(ToolExecutionResult.MODEL_CONTENT_OPTIONS).Length);
    }

    private JsonObject DescribeForModel(MailTexts texts, TimeZoneInfo timeZone)
    {
        var description = new JsonObject
        {
            ["mail_id"] = this.Summary.MailId,
            ["received"] = MailToolResults.FormatTime(this.Summary.ReceivedAtUtc, timeZone),
            ["from"] = texts[this.From],
            ["recipients"] = new JsonArray([..this.Recipients.Select(recipient => (JsonNode?)texts[recipient])]),
            ["subject"] = texts[this.Subject],
            ["folders"] = new JsonArray([..this.Folders.Select(folder => (JsonNode?)texts[folder])]),
            ["is_unread"] = !this.Summary.Flags.IsSeen,
            ["is_flagged"] = this.Summary.Flags.IsFlagged,
            ["is_answered"] = this.Summary.Flags.IsAnswered,
            ["importance"] = MailToolArguments.ToArgumentValue(this.Summary.Importance),
        };

        if (this.MoreRecipients > 0)
            description["more_recipients"] = this.MoreRecipients;

        if (this.MoreFolders > 0)
            description["more_folders"] = this.MoreFolders;

        if (this.SpecialFolder is { } specialFolder)
            description[MailToolArguments.SPECIAL_FOLDER_ARGUMENT] = specialFolder;

        if (this.Summary.EncryptionKind is not MailEncryptionKind.NONE)
            description["encryption"] = MailToolResults.GetEncryptionName(this.Summary.EncryptionKind);

        if (this.Attachments.Count > 0)
            description["attachments"] = new JsonArray([..this.Attachments.Select(attachment => (JsonNode?)texts[attachment])]);

        if (this.MoreAttachments > 0)
            description["more_attachments"] = this.MoreAttachments;

        if (this.Passage is { } passage)
        {
            description["passage"] = texts[passage];
            var attachment = this.LocatePassageInAttachment(texts);
            if (attachment != MailPassageAttachment.NONE)
                description["passage_location"] = attachment.Where is null ? $"attachment \"{attachment.Name}\"" : $"attachment \"{attachment.Name}\", {attachment.Where}";
        }

        return description;
    }

    /// <summary>
    /// Writes what the model received of this mail, for the user to read under its source.
    /// </summary>
    /// <param name="source">The source of this mail, see ToSource.</param>
    /// <param name="texts">The filtered texts of the result.</param>
    /// <param name="timeZone">The time zone of the user.</param>
    /// <returns>The passage of this mail.</returns>
    private SourcePassage ToPassage(Source source, MailTexts texts, TimeZoneInfo timeZone)
    {
        var attachment = this.LocatePassageInAttachment(texts);
        return source.ToMailPassage(MailToolResults.ComposePassage(
            texts[this.From],
            [..this.Recipients.Select(recipient => texts[recipient])],
            texts[this.Subject],
            MailToolResults.FormatTime(this.Summary.ReceivedAtUtc, timeZone),
            [..this.Attachments.Select(name => texts[name])],
            attachment == MailPassageAttachment.NONE ? null : attachment.Label,
            this.Passage is { } passage ? texts[passage] : null));
    }

    private Source ToSource(MailTexts texts, TimeZoneInfo timeZone)
    {
        var attachment = this.LocatePassageInAttachment(texts);
        return MailToolResults.CreateSource(
            this.Mailbox,
            this.Summary.MailId,
            texts[this.Subject],
            texts[this.Sender],
            this.Summary.ReceivedAtUtc,
            timeZone,
            attachment == MailPassageAttachment.NONE ? null : attachment.Label);
    }

    /// <summary>
    /// The attachment the passage is from, and where in it.
    /// </summary>
    /// <remarks>
    /// The attachments are numbered the way the index numbers them, cf. MailSummary.AttachmentNames.
    /// Where in an attachment is worded like the location of any other passage, cf.
    /// IRetrievalContextExtensions.DescribeLocation. A passage of the text of the mail names
    /// no attachment, and neither does one of a mail indexed before attachments were told apart.
    /// </remarks>
    /// <param name="texts">The filtered texts of the result.</param>
    /// <returns>The filtered name of the attachment, and where in it the passage is, if known; MailPassageAttachment.NONE when the passage is from no attachment.</returns>
    private MailPassageAttachment LocatePassageInAttachment(MailTexts texts)
    {
        if (this.PassageAttachment is not { } attachment || this.PassageLocation is not { AttachmentPosition: { } position } location)
            return MailPassageAttachment.NONE;

        var type = DataSourceLocalRetrievalService.GetRetrievalContentType(Path.GetExtension(this.Summary.AttachmentNames[position]));
        var located = DataSourceLocalRetrievalService.LocatePassage(location, type, chunkNumber: 1);
        return new(texts[attachment], IRetrievalContextExtensions.DescribeLocation(located.PageNumber, located.EndPageNumber, located.SheetName, located.StartRow, located.EndRow, located.StartLine, located.EndLine));
    }
}