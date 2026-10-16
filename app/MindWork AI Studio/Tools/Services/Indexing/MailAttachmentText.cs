using AIStudio.Tools.Mail;

using MailKit;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One attachment of a mail as a sync read it.
/// </summary>
/// <param name="Part">The attachment, from the structure of the mail.</param>
/// <param name="Name">Its file name, filtered for prompt injections, or empty when it has none.</param>
/// <param name="TextState">Whether its text was read and, when not, why.</param>
/// <param name="Content">Its text, which the runtime filtered while reading it. Empty unless the text was read.</param>
/// <param name="Strategy">How its text is cut into chunks, by its file type.</param>
internal sealed record MailAttachmentText(BodyPartBasic Part, string Name, MailPartTextState TextState, SegmentedText Content, ChunkingStrategy Strategy)
{
    private static readonly SegmentedText NO_TEXT = new(string.Empty, []);

    /// <summary>
    /// An attachment whose text was not read.
    /// </summary>
    /// <param name="part">The attachment, from the structure of the mail.</param>
    /// <param name="name">Its file name, filtered for prompt injections.</param>
    /// <param name="textState">Why its text was not read.</param>
    public static MailAttachmentText WithoutText(BodyPartBasic part, string name, MailPartTextState textState) => new(part, name, textState, NO_TEXT, TextChunker.DOCUMENT_STRATEGY);
}