using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A text with the words of a search marked, the same wherever the search shows what it found: in the
/// names of the workspace list, and in the passages of its tooltips.
/// </summary>
/// <remarks>
/// Without words, it shows the plain text. The mark has its look here, so a tooltip outside the list
/// shows it just like the list; a place may make it stronger, as the list does for its current result.
/// </remarks>
public partial class SearchHighlightedText : ComponentBase
{
    /// <summary>
    /// The text to show.
    /// </summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The words to mark, see <see cref="SearchText.ParseTerms"/>. None shows the plain text.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> Terms { get; set; } = [];
}