using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The passage of a chat in which the search found its words, with the words marked: the same in the
/// tooltip of the chat in the workspace list and in the tooltip of the line below the search field.
/// </summary>
/// <remarks>
/// Only the passage itself, whether from a message, the name of an attached file, or a source: the
/// name of the chat and where the words are show elsewhere already. The marks take the strong tint,
/// since the soft one hardly shows on the dark background of a tooltip.
/// </remarks>
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
    /// The words of the search, which the passage marks.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> Terms { get; set; } = [];
}