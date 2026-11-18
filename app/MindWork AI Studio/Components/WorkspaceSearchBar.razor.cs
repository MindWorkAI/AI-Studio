using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The results of the search in the list, at the top of the sidebar while the search field holds
/// words, see WorkspaceSearchField.
/// </summary>
/// <remarks>
/// It stays in place while the list below scrolls. It counts the results which the list marks, and
/// its arrows step from one result to the next, in the same yellow row as the find row of the open
/// chat, see SearchStepRow. The line below, inside the yellow, tells where the search found the
/// current result. It shows a progress line while the list searches inside the chats.
/// </remarks>
public partial class WorkspaceSearchBar : MSGComponentBase
{
    [Parameter, EditorRequired]
    public WorkspaceSearch Search { get; set; } = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.Search.TextChanged += this.OnSearchChanged;
        this.Search.IsSearchingContentsChanged += this.OnSearchChanged;
        this.Search.ResultsChanged += this.OnSearchChanged;
        await base.OnInitializedAsync();
    }

    #endregion

    /// <summary>
    /// Counts the results in the list. The block sits in the list, so it needs no words to set it apart
    /// from the matches in the open chat, which the find row of the chat counts.
    /// </summary>
    private string ResultText => this.Search.ResultCount == 0
        ? T("No results")
        : string.Format(T("Result {0} of {1}"), this.Search.CurrentResult, this.Search.ResultCount);

    /// <summary>
    /// The line below the count: where the search found the current result.
    /// </summary>
    /// <remarks>
    /// It explains the marks of the list, above all the magnifier of a chat which holds the words in its
    /// content. It tells only the name or the content, so it fits a narrow sidebar; where exactly the
    /// content holds the words, the passage in its tooltip shows, as does the tooltip of the chat in the list.
    /// </remarks>
    private string ReasonText => this.Search.CurrentReason.Kind switch
    {
        WorkspaceSearchReasonKind.NAME => T("Found in its name"),
        WorkspaceSearchReasonKind.CONTENT => T("Found in its content"),
        WorkspaceSearchReasonKind.NAME_AND_CONTENT => T("Found in its name and content"),

        _ when this.Search.IsSearchingContents => T("Searching inside the chats …"),

        // A result needs every word of the search, which the user may not expect:
        _ when SearchText.ParseTerms(this.Search.Text).Count > 1 => T("No chat or workspace holds all of these words"),
        _ => T("No chat or workspace holds this word"),
    };

    private void OnSearchChanged() => this.InvokeAsync(this.StateHasChanged).Observe($"{nameof(WorkspaceSearchBar)}: showing the state of the search");

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.Search.TextChanged -= this.OnSearchChanged;
        this.Search.IsSearchingContentsChanged -= this.OnSearchChanged;
        this.Search.ResultsChanged -= this.OnSearchChanged;
        base.DisposeResources();
    }

    #endregion
}