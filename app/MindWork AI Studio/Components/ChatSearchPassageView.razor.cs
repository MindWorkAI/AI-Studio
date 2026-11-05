using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Where in a chat the search found its words, and the passage there with the words marked: the same in
/// the tooltip of the chat in the workspace list and in the tooltip of the line below the search field.
/// </summary>
public partial class ChatSearchPassageView : ComponentBase
{
    /// <summary>
    /// How long the pointer rests before a tooltip of the search shows, so that tooltips do not flash
    /// while the pointer crosses the list.
    /// </summary>
    public const double TOOLTIP_DELAY = 500;

    /// <summary>
    /// The style of the content of a tooltip of the search: a passage wraps instead of running across
    /// the window.
    /// </summary>
    public const string TOOLTIP_CONTENT_STYLE = "max-width: 18rem; text-align: start;";

    /// <summary>
    /// Where in the chat the search found the words.
    /// </summary>
    [Parameter, EditorRequired]
    public ChatSearchPassage Passage { get; set; }

    /// <summary>
    /// Whether the chat was found by its name, so that this place holds the words as well.
    /// </summary>
    [Parameter]
    public bool IsAlso { get; set; }

    /// <summary>
    /// The words of the search, which the passage marks.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> Terms { get; set; } = [];
}