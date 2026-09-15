using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows the sources an answer rests on, grouped and numbered the way the export is.
/// </summary>
/// <remarks>
/// This list used to be Markdown, which read correctly but could not be clicked where it mattered:
/// a Markdown renderer hands every link to the browser, and the browser refuses a file address on a
/// page it loaded over http. A source of the user's own documents therefore did nothing at all.
/// Written out as components, an entry can hand its document to the runtime instead, together with
/// the page the passage was found on.
/// </remarks>
public partial class SourcesList : MSGComponentBase
{
    //
    // The name is about the alignment the function uses, not about the page: it brings the element
    // into view with its end at the bottom, which for a list at the end of an answer shows all of it.
    //
    private const string SCROLL_INTO_VIEW_FUNCTION = "scrollToBottom";

    /// <summary>
    /// The sources to show.
    /// </summary>
    [Parameter]
    public IList<Source> Sources { get; set; } = [];

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    private ILogger<SourcesList> Logger { get; init; } = null!;

    private readonly List<SourceEntryGroup> groups = [];

    private ElementReference listElement;

    /// <summary>
    /// Brings this list into view.
    /// </summary>
    /// <remarks>
    /// The counter above an answer says how many sources it rests on; this is how it takes the
    /// reader to them. The element stays here, where it is rendered, rather than being handed to
    /// whoever wants to scroll to it.
    /// </remarks>
    public async Task ScrollIntoViewAsync() => await this.JsRuntime.TryInvokeVoidAsync(this.CircuitState, SCROLL_INTO_VIEW_FUNCTION, this.listElement);

    #region Overrides of ComponentBase

    protected override async Task OnParametersSetAsync()
    {
        this.RebuildGroups();
        await base.OnParametersSetAsync();
    }

    #endregion

    /// <summary>
    /// Reads the sources once per render instead of once per entry and render.
    /// </summary>
    /// <remarks>
    /// Where a source points is answered by looking at its link, and while an answer streams, this
    /// runs again for every chunk. The previous Markdown list was rebuilt and parsed just as often,
    /// so this is the cheaper of the two, but it is still worth doing once for the whole list.
    /// </remarks>
    private void RebuildGroups()
    {
        this.groups.Clear();
        foreach (var group in this.Sources.GroupSources())
        {
            var entries = new List<SourceEntry>(group.Sources.Count);
            foreach (var numberedSource in group.Sources)
            {
                var document = numberedSource.Source.TryGetDocumentLocation(out var location) ? location : (SourceDocumentLocation?)null;
                entries.Add(new(numberedSource.Number, numberedSource.Source.Title, numberedSource.Source.URL, document));
            }

            this.groups.Add(new(group.Heading, entries));
        }
    }

    /// <summary>
    /// Opens a document in the program the system uses for it.
    /// </summary>
    /// <remarks>
    /// Whether the program can be sent to a page is the runtime's business, and it says afterwards
    /// whether it managed to. Nothing is shown about that here: the document is open, and the title
    /// of the source names the page anyway.
    /// </remarks>
    /// <param name="document">The document to open, and the page to show.</param>
    private async Task OpenDocument(SourceDocumentLocation document)
    {
        OpenDocumentResponse response;
        try
        {
            response = await this.RustService.TryOpenDocumentInSystemViewer(document.Path, document.PageNumber);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not open a source document.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.Description, T("Could not open the document.")));
            return;
        }

        if (response.Success)
            return;

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
        await this.MessageBus.SendError(new(Icons.Material.Filled.Description, string.Format(T("Could not open the document: {0}"), issue)));
    }

    /// <summary>
    /// Opens the file browser of the system and selects the document in it.
    /// </summary>
    /// <remarks>
    /// The second way out of the list: a document which the system opens in the wrong program, or
    /// which the user wants to move or send on instead of read, is reached from here without being
    /// opened. This is the same way out the embeddings page offers for a file it could not read.
    /// </remarks>
    /// <param name="document">The document to show.</param>
    private async Task ShowInFileManager(SourceDocumentLocation document)
    {
        OpenPathResponse response;
        try
        {
            response = await this.RustService.TryOpenPathInRuntimeFileManager(document.Path);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not show a source document in the file manager.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.FolderOpen, T("Could not open the file location.")));
            return;
        }

        if (response.Success)
            return;

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
        await this.MessageBus.SendError(new(Icons.Material.Filled.FolderOpen, string.Format(T("Could not open the file location: {0}"), issue)));
    }

    /// <summary>
    /// One group of the list, prepared so that the markup only has to show it.
    /// </summary>
    /// <param name="Heading">The heading above the group.</param>
    /// <param name="Entries">The entries of the group, in the order they are shown.</param>
    private readonly record struct SourceEntryGroup(string Heading, IReadOnlyList<SourceEntry> Entries);

    /// <summary>
    /// One entry of the list, prepared so that the markup only has to show it.
    /// </summary>
    /// <param name="Number">The number the source is listed under.</param>
    /// <param name="Title">The title of the source.</param>
    /// <param name="Link">The address of the source, which a web source is opened by.</param>
    /// <param name="Document">The document the source names, or null when it names none.</param>
    private readonly record struct SourceEntry(int Number, string Title, string Link, SourceDocumentLocation? Document);
}