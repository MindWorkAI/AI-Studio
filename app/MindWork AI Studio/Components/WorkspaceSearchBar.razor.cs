using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Components;

/// <summary>
/// The field of the workspace search, as the second row of the sidebar's header.
/// </summary>
/// <remarks>
/// It stays in place while the list below scrolls. It hands what the user types to the list through
/// the search. Beside the field, it counts the results which the list marks, and its arrows, Enter, and
/// Shift+Enter step from one result to the next. It shows a progress line while the list searches inside
/// the chats. The clear button of the field and Escape take back the text; Escape in an empty field, or
/// the magnifier button in the header of the sidebar, close the search.
/// </remarks>
public partial class WorkspaceSearchBar : MSGComponentBase
{
    [Parameter, EditorRequired]
    public WorkspaceSearch Search { get; set; } = null!;

    [Parameter]
    public EventCallback CloseRequested { get; set; }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.Search.IsSearchingContentsChanged += this.OnSearchChanged;
        this.Search.ResultsChanged += this.OnSearchChanged;
        await base.OnInitializedAsync();
    }

    #endregion

    /// <summary>
    /// Counts the results in the list, which the words "in list" set apart from the matches in the open chat.
    /// </summary>
    private string ResultText => this.Search.ResultCount == 0
        ? T("None in list")
        : string.Format(T("{0}/{1} in list"), this.Search.CurrentResult, this.Search.ResultCount);

    /// <summary>
    /// The line below the field: where the search found the current result, or a tip before the user
    /// typed anything.
    /// </summary>
    /// <remarks>
    /// It explains the marks of the list, above all the magnifier of a chat which holds the words
    /// inside, which shows here too. It names the place only, so it fits a narrow sidebar; the passage
    /// shows in the tooltip of the chat in the list.
    /// </remarks>
    private string ReasonText => this.Search.CurrentReason.Kind switch
    {
        WorkspaceSearchReasonKind.WORKSPACE_NAME => T("Found in the name of the workspace"),
        WorkspaceSearchReasonKind.CHAT_NAME => T("Found in the name of the chat"),
        WorkspaceSearchReasonKind.IN_CHAT => this.Search.CurrentReason.Passage.Label(isAlso: false),
        WorkspaceSearchReasonKind.ALSO_IN_CHAT => this.Search.CurrentReason.Passage.Label(isAlso: true),

        _ when string.IsNullOrWhiteSpace(this.Search.Text) => T("Put words in quotes to find an exact phrase"),
        _ when this.Search.IsSearchingContents => T("Searching inside the chats …"),

        // A result needs every word of the search, which the user may not expect:
        _ when SearchText.ParseTerms(this.Search.Text).Count > 1 => T("No chat or workspace holds all of these words"),
        _ => T("No chat or workspace holds this word"),
    };

    /// <summary>
    /// Whether the line gets a tooltip: before the user types, it tells what the search covers, which the
    /// line has no room for next to the tip about quotes; for a chat which holds the words inside, it
    /// shows where, with the passage. Every other line says all there is.
    /// </summary>
    private bool HasReasonTooltip => this.Search.CurrentReason.IsInChat || string.IsNullOrWhiteSpace(this.Search.Text);

    private void TextChanged(string? text) => this.Search.SetText(text);

    private async Task KeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            // Escape takes back what the user typed first, as the clear button of the field does, and
            // closes the search only when nothing is left:
            case "Escape" when !string.IsNullOrEmpty(this.Search.Text):
                this.Search.SetText(string.Empty);
                break;

            case "Escape":
                await this.CloseRequested.InvokeAsync();
                break;

            case "Enter":
                this.Search.Step(args.ShiftKey ? -1 : 1);
                break;
        }
    }

    private void OnSearchChanged() => this.InvokeAsync(this.StateHasChanged).Observe($"{nameof(WorkspaceSearchBar)}: showing the state of the search");

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.Search.IsSearchingContentsChanged -= this.OnSearchChanged;
        this.Search.ResultsChanged -= this.OnSearchChanged;
        base.DisposeResources();
    }

    #endregion
}