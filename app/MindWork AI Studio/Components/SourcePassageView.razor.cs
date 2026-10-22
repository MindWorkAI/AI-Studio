using AIStudio.Tools.RAG;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows one passage the AI received from a data source, in full and the way the AI read it.
/// </summary>
/// <remarks>
/// The content is the filtered one, see SourcePassage. The bar above it says where in the document
/// the passage stands, and whether it is the transcript of a recording, since the AI then read the
/// words of the recording rather than the recording itself. An image the AI was only told about is
/// said to be one, rather than shown as if the AI had seen it.
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
    private RustService RustService { get; init; } = null!;

    private string heading = string.Empty;

    private string text = string.Empty;

    /// <summary>
    /// The document, at the page of the passage where it has one, or null when there is no document to open.
    /// </summary>
    private SourceDocumentLocation? documentAtPassage;

    private string openTooltip = string.Empty;

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        var parts = new[] { this.Passage.GetLocationLabel(), this.GetTranscriptLabel() }.Where(part => part.Length > 0).ToList();
        this.heading = parts.Count > 0 ? string.Join(" · ", parts) : T("Text the AI received");

        //
        // A document opens at the page of the passage. One without pages, such as a CSV file, opens
        // at its beginning: no program can be sent to a part, but the user may still want to look.
        //
        var page = this.Passage.PageNumber is > 0 ? this.Passage.PageNumber : null;
        this.documentAtPassage = this.Document is { } document ? document with { PageNumber = page } : null;
        this.openTooltip = page is null ? T("Opens the document") : string.Format(T("Opens the document at page {0}"), page);

        //
        // The surrounding content follows the matched text, the way the AI received both, so that
        // all of it reads in one place:
        //
        this.text = this.Passage.SurroundingContent.Count is 0
            ? this.Passage.Text
            : $"{this.Passage.Text}{PART_SEPARATOR}**{T("Surrounding content")}**{PART_SEPARATOR}{string.Join(PART_SEPARATOR, this.Passage.SurroundingContent)}";

        base.OnParametersSet();
    }

    #endregion

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