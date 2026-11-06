using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The sign of the chat search: a magnifier on a disc of its yellow. It marks a chat which holds the
/// words of the search inside, in the workspace list, and introduces what the search says about it, in
/// the line below the search field and in the row which finds in the open chat.
/// </summary>
/// <remarks>
/// The magnifier keeps the color of the text around it, while the disc carries the yellow: a yellow icon
/// would hardly show on white. It takes its size from the font size around it, so each place gets a
/// fitting one without a rule of its own.
/// </remarks>
public partial class SearchFoundIcon : ComponentBase
{
    /// <summary>
    /// Whether the disc takes the full yellow, as for the current result of the search.
    /// </summary>
    [Parameter]
    public bool IsStrong { get; set; }
}