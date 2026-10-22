using AIStudio.Dialogs;
using AIStudio.Tools.RAG;
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

    /// <summary>
    /// The passages the AI received from the sources, which the user can read below each of them.
    /// </summary>
    [Parameter]
    public IReadOnlyList<SourcePassage> Passages { get; set; } = [];

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

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
        var passagesBySource = this.Passages.SortBySource();
        foreach (var group in this.Sources.GroupSources())
        {
            var entries = new List<SourceEntry>(group.Sources.Count);
            foreach (var numberedSource in group.Sources)
            {
                var document = numberedSource.Source.TryGetDocumentLocation(out var location) ? location : (SourceDocumentLocation?)null;
                var passages = passagesBySource.GetPassagesOf(numberedSource.Source);

                //
                // Pages are links only into a document, and only under the name the title of the
                // source was made with, see SourcePassageExtensions.NamePassagesInTitles. Parts of a
                // document without pages are nothing a program can open, so their title stays as it is:
                //
                var name = passages.Count > 0 ? passages[0].DataSourceName : string.Empty;
                var pages = document is not null && !string.IsNullOrWhiteSpace(name) && passages.GetLocation() is { ArePages: true } pageLocation ? pageLocation : null;

                entries.Add(new(numberedSource.Number, numberedSource.Source.Title, numberedSource.Source.URL, document, numberedSource.Source.IsMailSource(), passages, name, pages));
            }

            this.groups.Add(new(group.Heading, entries));
        }
    }

    private string PassagesTooltip(SourceEntry entry) => entry.Passages.Count is 1
        ? T("Show the passage the AI received from this source")
        : string.Format(T("Show the {0} passages the AI received from this source"), entry.Passages.Count);

    private async Task ShowPassages(SourceEntry entry) => await SourcePassagesDialog.OpenDialogAsync(this.DialogService, entry.Passages, entry.Document);

    private async Task OpenDocument(SourceDocumentLocation document) => await document.OpenAsync(this.RustService);

    private async Task ShowInFileManager(SourceDocumentLocation document) => await document.ShowInFileManagerAsync(this.RustService);

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
    /// <param name="IsMail">Whether the source names a mail, which is shown as text.</param>
    /// <param name="Passages">The passages the AI received from the source; empty when it received none, or the chat is older than these.</param>
    /// <param name="Name">The name of the source without its location, or empty when it has no passages.</param>
    /// <param name="Location">The pages of the passages in the document, each to be opened on its own, or null when there are none.</param>
    private readonly record struct SourceEntry(int Number, string Title, string Link, SourceDocumentLocation? Document, bool IsMail, IReadOnlyList<SourcePassage> Passages, string Name, PassageLocation? Location);
}