namespace AIStudio.Components;

/// <summary>
/// Where the search found the words of a result, see <see cref="WorkspaceSearchReason"/>.
/// </summary>
public enum WorkspaceSearchReasonKind
{
    /// <summary>
    /// There is no current result.
    /// </summary>
    NONE,

    /// <summary>
    /// The result is a workspace whose name holds all words.
    /// </summary>
    WORKSPACE_NAME,

    /// <summary>
    /// The result is a chat whose own name holds all words.
    /// </summary>
    CHAT_NAME,

    /// <summary>
    /// The result is a chat whose messages, attached files, or sources hold the words its name lacks.
    /// </summary>
    IN_CHAT,

    /// <summary>
    /// The result is a chat found by its name, whose messages, attached files, or sources hold words of
    /// the search as well.
    /// </summary>
    ALSO_IN_CHAT,
}