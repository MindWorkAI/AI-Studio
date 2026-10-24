using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows one passage the AI received from a data source, in full and the way the AI read it.
/// </summary>
/// <remarks>
/// The content is the filtered one, see SourcePassage. The bar above it says where in the document
/// the passage stands, and whether it is the transcript of a recording, since the AI then read the
/// words of the recording rather than the recording itself. An image the AI was only told about is
/// said to be one, rather than shown as if the AI had seen it.<br/><br/>
///
/// The texts of a passage lie in the provenance store once its answer is complete, and are read
/// from there. Should the store no longer hold them, the view says so instead of showing nothing.
/// </remarks>
public partial class SourcePassageView : MSGComponentBase
{
    /// <summary>
    /// What stands between the parts of the text, as Markdown: a horizontal rule.
    /// </summary>
    private const string PART_SEPARATOR = "\n\n---\n\n";

    /// <summary>
    /// The passage to show.
    /// </summary>
    [Parameter]
    public SourcePassage Passage { get; set; } = new();

    /// <summary>
    /// The document the passage is from, which can be opened at its page, or null when there is no
    /// document to open.
    /// </summary>
    [Parameter]
    public SourceDocumentLocation? Document { get; set; }

    /// <summary>
    /// How high the text may grow before it scrolls, as a CSS length.
    /// </summary>
    [Parameter]
    public string MaxHeight { get; set; } = "40vh";

    [Inject]
    private DatabaseClientProvider DatabaseClientProvider { get; init; } = null!;

    [Inject]
    private ILogger<SourcePassageView> Logger { get; init; } = null!;

    private string heading = string.Empty;

    private string text = string.Empty;

    /// <summary>
    /// What the bar above the text opens: the document, or the web page of the passage; empty when there is neither.
    /// </summary>
    private string openPath = string.Empty;

    /// <summary>
    /// The page of the passage, which a document opens at, or null.
    /// </summary>
    private int? openPage;

    /// <summary>
    /// The tooltip of opening a document, or null for a web page, whose tooltip FileActions knows.
    /// </summary>
    private string? openTooltip;

    /// <summary>
    /// The passage whose texts are shown or being read, so that a render with the same passage does not read them again.
    /// </summary>
    private SourcePassage? shownPassage;

    /// <summary>
    /// Numbers the reads, so that a read which another passage overtook leaves the texts alone.
    /// </summary>
    private int readNumber;

    private bool isReadingText;

    /// <summary>
    /// Whether the provenance store no longer holds the texts of the passage.
    /// </summary>
    private bool isTextMissing;

    #region Overrides of ComponentBase

    protected override async Task OnParametersSetAsync()
    {
        var parts = new[] { this.Passage.GetLocationLabel(), this.GetTranscriptLabel() }.Where(part => part.Length > 0).ToList();
        this.heading = parts.Count > 0 ? string.Join(" · ", parts) : T("Text the AI received");

        //
        // A document opens at the page of the passage. One without pages, such as a CSV file, opens
        // at its beginning: no program can be sent to a part, but the user may still want to look.
        // Without a document, the passage of a web page opens its page; any other passage, such as
        // that of a mail, has nothing to open, see FileActions:
        //
        var page = this.Passage.PageNumber is > 0 ? this.Passage.PageNumber : null;
        this.openPath = this.Document?.Path ?? this.Passage.Path;
        this.openPage = this.Document is null ? null : page;
        this.openTooltip = this.Document is null ? null : page is null ? T("Opens the document") : string.Format(T("Opens the document at page {0}"), page);

        if (!ReferenceEquals(this.shownPassage, this.Passage))
        {
            this.shownPassage = this.Passage;
            await this.ReadTextAsync(this.Passage);
        }

        await base.OnParametersSetAsync();
    }

    #endregion

    /// <summary>
    /// Reads the texts of a passage: from the provenance store once they lie there, otherwise from the passage itself.
    /// </summary>
    /// <param name="passage">The passage.</param>
    private async Task ReadTextAsync(SourcePassage passage)
    {
        var currentRead = ++this.readNumber;
        this.isTextMissing = false;
        if (passage.ProvenanceId is not { } provenanceId)
        {
            this.isReadingText = false;
            this.text = this.ComposeText(passage.Text, passage.SurroundingContent);
            return;
        }

        this.isReadingText = true;
        this.text = string.Empty;

        ProvenanceEntry? entry = null;
        try
        {
            var store = await this.DatabaseClientProvider.GetProvenanceStoreAsync();
            var entries = await store.GetEntriesAsync([provenanceId], CancellationToken.None);
            entry = entries.GetValueOrDefault(provenanceId);
        }
        catch (Exception exception)
        {
            this.Logger.LogError(exception, "Failed to read the passage '{ProvenanceId}' from the provenance store.", provenanceId);
        }

        // The user went on to another passage meanwhile, which shows its own texts:
        if (currentRead != this.readNumber)
            return;

        this.isReadingText = false;
        this.isTextMissing = entry is null;
        this.text = entry is null ? string.Empty : this.ComposeText(entry.Content, entry.SurroundingContent);
    }

    /// <summary>
    /// Writes the texts of a passage as one text.
    /// </summary>
    /// <remarks>
    /// The surrounding content follows the matched text, the way the AI received both, so that all
    /// of it reads in one place.
    /// </remarks>
    private string ComposeText(string matchedText, IReadOnlyList<string> surroundingContent) => surroundingContent.Count is 0
        ? matchedText
        : $"{matchedText}{PART_SEPARATOR}**{T("Surrounding content")}**{PART_SEPARATOR}{string.Join(PART_SEPARATOR, surroundingContent)}";

    /// <summary>
    /// Says what the passage is a transcript of, or nothing when it is no transcript.
    /// </summary>
    /// <remarks>
    /// Asked by the extension of the path only: the file may be gone by now, and reading it would
    /// tell nothing more about what the AI received.
    /// </remarks>
    private string GetTranscriptLabel()
    {
        if (!this.Passage.IsTranscript())
            return string.Empty;

        var extension = Path.GetExtension(this.Passage.Path);
        if (FileTypes.IsAllowedExtension(extension, FileTypes.VIDEO))
            return T("Transcript of a video file");

        return FileTypes.IsAllowedExtension(extension, FileTypes.AUDIO)
            ? T("Transcript of an audio file")
            : T("Transcript");
    }
}