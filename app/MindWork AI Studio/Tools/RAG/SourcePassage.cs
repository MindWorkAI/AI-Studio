using System.Text.Json.Serialization;

namespace AIStudio.Tools.RAG;

/// <summary>
/// One passage a data source handed to the AI for an answer, as the AI received it.
/// </summary>
/// <remarks>
/// Stored with the answer, so the user can still read what the answer rests on after the chat was
/// reloaded: where it is with the answer, its texts in the provenance store, see ProvenanceId. The
/// texts are the filtered ones: whatever the prompt injection filter removed did not reach the AI,
/// so it is not shown as if it had.<br/><br/>
/// A passage belongs to the source it is listed under, which is why it is only made for a context
/// with a source, see SourceUrl. A context which names nothing to open, e.g., by a path which only
/// its ERI server knows, gets a source named as text, see IRetrievalContextExtensions.ToSources.<br/><br/>
/// Plain stored values only: a computed property would be written into every chat file as well.
/// </remarks>
public sealed record SourcePassage
{
    /// <summary>
    /// The address of the source this passage is listed under.
    /// </summary>
    /// <remarks>
    /// The first source of the context, i.e., its own reference, such as the file with the page of
    /// the passage. The list of sources tells sources apart by their address without the fragment,
    /// and so does the search for the passages of a source, see GetPassagesOf.
    /// </remarks>
    public string SourceUrl { get; init; } = string.Empty;

    /// <summary>
    /// The name of where the passage was found, e.g., a document or a database.
    /// </summary>
    public string DataSourceName { get; init; } = string.Empty;

    /// <summary>
    /// The path to the content, e.g., a file path or a URL.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// The category of the content, like text or image.
    /// </summary>
    public RetrievalContentCategory Category { get; init; }

    /// <summary>
    /// What type of content the passage is, like a document or a transcript.
    /// </summary>
    public RetrievalContentType Type { get; init; }

    /// <summary>
    /// The entry of the provenance store which holds the matched text and the content around it,
    /// or null when the passage holds them itself.
    /// </summary>
    /// <remarks>
    /// Set once the answer is complete, see ContentText.StoreReceivedContentAsync. Until then, and
    /// whenever the store could not take them, the passage holds both texts itself; once they are
    /// stored, Text and SurroundingContent are empty. Left out of the chat file while null.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ProvenanceId { get; init; }

    /// <summary>
    /// The matched text, filtered for prompt injections, as long as it is not stored, see ProvenanceId.
    /// </summary>
    /// <remarks>
    /// Empty for an image which matched: the AI is only told that it did, never shown the image.
    /// </remarks>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// The content around the matched text, filtered for prompt injections, as long as it is not stored, see ProvenanceId.
    /// </summary>
    public IReadOnlyList<string> SurroundingContent { get; init; } = [];

    /// <summary>
    /// The page the passage starts on, or null when it has none.
    /// </summary>
    public int? PageNumber { get; init; }

    /// <summary>
    /// The page the passage ends on, or null when it is not known.
    /// </summary>
    public int? EndPageNumber { get; init; }

    //
    // Where a passage without pages is. Left out of the chat file while unknown, which they are for
    // every passage of a document with pages, and for every passage stored before they existed:
    //

    /// <summary>
    /// The sheet of a spreadsheet the passage is from, or null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SheetName { get; init; }

    /// <summary>
    /// The row the passage starts at, counted from one: a row of its sheet, or of a table such as a CSV file.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StartRow { get; init; }

    /// <summary>
    /// The row the passage ends at, or null when it is not known.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EndRow { get; init; }

    /// <summary>
    /// The line the passage starts at, counted from one, for text and source code, or null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StartLine { get; init; }

    /// <summary>
    /// The line the passage ends at, or null when it is not known.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EndLine { get; init; }

    /// <summary>
    /// Which part of its document the passage is, counted from one, or null when the data source
    /// does not say.
    /// </summary>
    /// <remarks>
    /// Names the place of a passage which says neither pages, nor rows, nor lines, e.g., one
    /// indexed before these were kept. Wherever they are known, they say it better, see
    /// SourcePassageExtensions.GetLocation.
    /// </remarks>
    public int? ChunkNumber { get; init; }
}