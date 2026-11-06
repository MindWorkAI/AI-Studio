namespace AIStudio.Tools;

/// <summary>
/// A chat which a search found.
/// </summary>
/// <param name="Chat">The chat.</param>
/// <param name="NameMatched">Whether its own name holds all words.</param>
/// <param name="Passage">Where the chat holds the words, when its messages, attached files, or sources hold them: for a
/// chat found through them, a word its name lacks; for a chat found by its name, any word of the search. Otherwise null.</param>
public readonly record struct WorkspaceSearchResult(WorkspaceTreeChat Chat, bool NameMatched, ChatSearchPassage? Passage);