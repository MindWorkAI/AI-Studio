namespace AIStudio.Components;

/// <summary>
/// Why the search found the current result, which the field shows below itself.
/// </summary>
/// <param name="Kind">Where the search found the words.</param>
/// <param name="Passage">For a chat whose messages, attached files, or sources hold the words, where it holds the first one.</param>
public readonly record struct WorkspaceSearchReason(WorkspaceSearchReasonKind Kind, ChatSearchPassage Passage = default)
{
    public static readonly WorkspaceSearchReason NONE = new(WorkspaceSearchReasonKind.NONE);

    /// <summary>
    /// Whether the messages, attached files, or sources of the result hold the words, which shows the
    /// passage and the magnifier.
    /// </summary>
    public bool IsInChat => this.Kind is WorkspaceSearchReasonKind.IN_CHAT or WorkspaceSearchReasonKind.ALSO_IN_CHAT;
}