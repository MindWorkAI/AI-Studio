using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A row in the yellow of the search, which tells how far the user got through what the search found,
/// and steps from one to the next with its arrows: in the open chat, see ChatFindBar, and in the list
/// of workspaces, see WorkspaceSearchBar.
/// </summary>
/// <remarks>
/// Both look alike on purpose, since both do the same: they count what the search found, and step
/// through it. What belongs to the count, such as why the list found its current result, goes into
/// ChildContent, below the row and inside its yellow.
/// </remarks>
public partial class SearchStepRow : MSGComponentBase
{
    /// <summary>
    /// What the row says, such as how many results there are and which one is the current one.
    /// </summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether there is anything to step to.
    /// </summary>
    [Parameter]
    public bool CanStep { get; set; }

    /// <summary>
    /// The tooltip and the accessible name of the arrow to the previous result.
    /// </summary>
    [Parameter, EditorRequired]
    public string PreviousLabel { get; set; } = string.Empty;

    /// <summary>
    /// The tooltip and the accessible name of the arrow to the next result.
    /// </summary>
    [Parameter, EditorRequired]
    public string NextLabel { get; set; } = string.Empty;

    /// <summary>
    /// Asks to step to the previous result, with -1, or to the next one, with 1.
    /// </summary>
    [Parameter]
    public EventCallback<int> StepRequested { get; set; }

    /// <summary>
    /// What shows below the row, inside its yellow.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Classes for the outside of the row, such as its margins.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Styles for the outside of the row, such as margins which no class of MudBlazor gives.
    /// </summary>
    [Parameter]
    public string? Style { get; set; }

    private string Classes => string.IsNullOrWhiteSpace(this.Class) ? "search-step-row" : $"search-step-row {this.Class}";
}