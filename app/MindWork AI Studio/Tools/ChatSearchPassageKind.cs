namespace AIStudio.Tools;

/// <summary>
/// Where in a chat the search found a word, see <see cref="ChatSearchPassage"/>.
/// </summary>
public enum ChatSearchPassageKind
{
    /// <summary>
    /// In the text of one of its messages.
    /// </summary>
    MESSAGE,

    /// <summary>
    /// In the name of a file attached to one of its messages.
    /// </summary>
    ATTACHED_FILE,

    /// <summary>
    /// In the title or the address of one of its sources.
    /// </summary>
    SOURCE,
}