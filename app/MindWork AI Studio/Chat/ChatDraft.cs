using System.Text.Json.Serialization;

namespace AIStudio.Chat;

/// <summary>
/// The message of a chat which the user has not sent yet, with its text and its attachments.
/// </summary>
/// <remarks>
/// The draft is stored with its chat, so the message is still there when the user comes back to the
/// chat.<br/><br/>
///
/// A managed transcript always belongs to exactly one owner: to a sent message, or to the draft.
/// Sending hands the attachments of the draft to the new message, and editing a message is the way
/// back. While a transcript belongs to the draft, removing it from the composer deletes its file, and
/// opening the chat again brings it back into the composer.<br/><br/>
///
/// The draft is a class rather than a record on purpose. A copy of its thread made with a with
/// expression is still the same chat, e.g., after the user picked another profile, so both copies have
/// to share one draft. Several parties change the draft at the same time, too: the user in the
/// composer, and the media transcription service, which adds a finished transcript from the thread
/// pool. Meanwhile, a running AI job may store the thread. That is why every change replaces the text
/// or the list of attachments under a lock instead of changing the list in place: whoever reads the
/// draft, the serializer included, always gets a complete list.
/// </remarks>
public sealed class ChatDraft
{
    private readonly Lock stateLock = new();

    /// <summary>
    /// The text of the message.
    /// </summary>
    [JsonInclude]
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// The files attached to the message, the transcripts of audio and video files included.
    /// </summary>
    /// <remarks>
    /// Declared with the base type, so every attachment is stored together with its kind, and a
    /// transcript is still a transcript after the chat was loaded again.
    /// </remarks>
    [JsonInclude]
    public IReadOnlyList<FileAttachment> FileAttachments { get; private set; } = [];

    /// <summary>
    /// The texts a preview read of the attached files, which sending takes along instead of reading the files again.
    /// </summary>
    /// <remarks>
    /// Kept apart from the attachments, which stay as the user attached them: the composer, this
    /// draft, and the attachment dialogs tell attachments apart by their values, and one which changed
    /// underneath them would count as removed and added. Stored with the chat like everything else of
    /// the draft, so a preview still counts when the user comes back to the chat. The provenance
    /// store holds the texts themselves.
    /// </remarks>
    [JsonInclude]
    public IReadOnlyList<PreviewedText> PreviewedTexts { get; private set; } = [];

    /// <summary>
    /// Whether the draft holds neither text nor attachments.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(this.Text) && this.FileAttachments.Count == 0;

    /// <summary>
    /// The transcripts among the attachments, whose files the chat manages itself.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ManagedTranscriptAttachment> MediaTranscripts => this.FileAttachments.OfType<ManagedTranscriptAttachment>().ToList();

    /// <summary>
    /// Replaces the text of the message.
    /// </summary>
    /// <param name="text">The text as the user wrote it.</param>
    public void SetText(string? text)
    {
        lock (this.stateLock)
            this.Text = text ?? string.Empty;
    }

    /// <summary>
    /// Takes over the attachments the composer shows.
    /// </summary>
    /// <remarks>
    /// Transcripts are only ever added here, never removed. The media transcription service hands a
    /// finished transcript to the draft before the composer shows it, so a draft which simply took over
    /// what the composer shows would lose such a transcript, and its file would stay behind without an
    /// owner. A transcript leaves the draft when the user removes it, see RemoveAttachments.
    /// </remarks>
    /// <param name="composerAttachments">The attachments the composer shows.</param>
    public void MirrorAttachments(IEnumerable<FileAttachment> composerAttachments)
    {
        lock (this.stateLock)
            this.FileAttachments = Combine(composerAttachments, this.MediaTranscripts);
    }

    /// <summary>
    /// Adds attachments the draft does not hold yet.
    /// </summary>
    /// <param name="attachments">The attachments to add. One whose path the draft already holds is left out.</param>
    /// <returns>True when at least one attachment was added.</returns>
    public bool AddAttachments(IEnumerable<FileAttachment> attachments)
    {
        lock (this.stateLock)
        {
            var combined = Combine(this.FileAttachments, attachments);
            if (combined.Count == this.FileAttachments.Count)
                return false;

            this.FileAttachments = combined;
            return true;
        }
    }

    /// <summary>
    /// Removes attachments, which is what the user does by taking them out of the composer.
    /// </summary>
    /// <param name="attachments">The attachments to remove. They are found by their path, so a newer state of the same file is removed as well.</param>
    public void RemoveAttachments(IEnumerable<FileAttachment> attachments)
    {
        var removedPaths = attachments.Select(attachment => attachment.FilePath).ToHashSet(StringComparer.Ordinal);
        lock (this.stateLock)
            this.FileAttachments = this.FileAttachments.Where(attachment => !removedPaths.Contains(attachment.FilePath)).ToList();
    }

    /// <summary>
    /// Replaces every attachment with what the given function makes of it.
    /// </summary>
    /// <remarks>
    /// Meant for files which changed their place, e.g., because the chat moved into another workspace
    /// or was copied, together with its transcripts.
    /// </remarks>
    /// <param name="replace">Gives the attachment to keep in place of the given one, or the given one itself when nothing changes.</param>
    public void ReplaceAttachments(Func<FileAttachment, FileAttachment> replace)
    {
        lock (this.stateLock)
            this.FileAttachments = this.FileAttachments.Select(replace).ToList();
    }

    /// <summary>
    /// Makes a sent message the draft again, which is what editing a message does.
    /// </summary>
    /// <remarks>
    /// Text and attachments of the message replace what the draft held so far, except for its
    /// transcripts: the draft is their only owner, so dropping them would leave their files behind.
    /// </remarks>
    /// <param name="message">The message to edit.</param>
    public void RestoreFromMessage(ContentText message)
    {
        lock (this.stateLock)
        {
            this.Text = message.Text;
            this.FileAttachments = Combine(message.FileAttachments, this.MediaTranscripts);
        }
    }

    /// <summary>
    /// Finds the text a preview read of an attached file.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>The text, or null when no preview read the file.</returns>
    public PreviewedText? GetPreviewedText(string filePath)
    {
        lock (this.stateLock)
            return this.PreviewedTexts.FirstOrDefault(text => string.Equals(text.FilePath, filePath, StringComparison.Ordinal));
    }

    /// <summary>
    /// Notes the text a preview read of an attached file, in place of one noted for the same file before.
    /// </summary>
    /// <param name="previewedText">The text.</param>
    /// <returns>The entry of the text noted before, which nothing refers to any longer, or null.</returns>
    public Guid? NotePreviewedText(PreviewedText previewedText)
    {
        lock (this.stateLock)
        {
            var replaced = this.PreviewedTexts.FirstOrDefault(text => string.Equals(text.FilePath, previewedText.FilePath, StringComparison.Ordinal));
            this.PreviewedTexts = [..this.PreviewedTexts.Where(text => !ReferenceEquals(text, replaced)), previewedText];
            return replaced?.ProvenanceId;
        }
    }

    /// <summary>
    /// Empties the draft, which is what sending it does: its attachments belong to the new message from then on.
    /// </summary>
    /// <remarks>
    /// The texts a preview read go as well. Whoever sends the draft removes those the message went
    /// without from the provenance store.
    /// </remarks>
    public void Clear()
    {
        lock (this.stateLock)
        {
            this.Text = string.Empty;
            this.FileAttachments = [];
            this.PreviewedTexts = [];
        }
    }

    /// <summary>
    /// Puts two lists of attachments together, keeping the first attachment of every path.
    /// </summary>
    private static List<FileAttachment> Combine(IEnumerable<FileAttachment> first, IEnumerable<FileAttachment> second)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        return first.Concat(second).Where(attachment => paths.Add(attachment.FilePath)).ToList();
    }
}