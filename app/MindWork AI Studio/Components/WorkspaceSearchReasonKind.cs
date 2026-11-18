namespace AIStudio.Components;

/// <summary>
/// Where the search found the words of a result, see <see cref="WorkspaceSearchReason"/>.
/// </summary>
/// <remarks>
/// Only the name and the content count apart, which is all the line below the count of the results
/// tells. Where exactly the content holds the words, in a message, sent or not, the name of an
/// attached file, or a source, shows in its tooltip, which shows the passage itself.
/// </remarks>
public enum WorkspaceSearchReasonKind
{
    /// <summary>
    /// There is no current result.
    /// </summary>
    NONE,

    /// <summary>
    /// The result is a workspace or a chat whose own name holds all words.
    /// </summary>
    NAME,

    /// <summary>
    /// The result is a chat whose content holds the words its name lacks: its messages, sent or not, the
    /// names of its attached files, or its sources.
    /// </summary>
    CONTENT,

    /// <summary>
    /// The result is a chat found by its name, whose content holds words of the search as well.
    /// </summary>
    NAME_AND_CONTENT,
}