namespace AIStudio.Components;

/// <summary>
/// Why the search found the current result, which the results at the top of the list show below their count.
/// </summary>
/// <param name="Kind">Where the search found the words.</param>
/// <param name="Passage">For a chat whose content holds the words, where it holds the first one.</param>
public readonly record struct WorkspaceSearchReason(WorkspaceSearchReasonKind Kind, ChatSearchPassage Passage = default)
{
    public static readonly WorkspaceSearchReason NONE = new(WorkspaceSearchReasonKind.NONE);

    /// <summary>
    /// Whether the content of the result holds the words, which shows the passage and the magnifier.
    /// </summary>
    public bool IsInContent => this.Kind is WorkspaceSearchReasonKind.CONTENT or WorkspaceSearchReasonKind.NAME_AND_CONTENT;
}