namespace AIStudio.Tools;

/// <summary>
/// What shows where in a chat the search found a word: the passage of a message, the name of an
/// attached file, or the title and address of a source.
/// </summary>
/// <param name="Kind">Where the search found the word.</param>
/// <param name="Text">The text to show, in which the words of the search get marked.</param>
public readonly record struct ChatSearchPassage(ChatSearchPassageKind Kind, string Text);