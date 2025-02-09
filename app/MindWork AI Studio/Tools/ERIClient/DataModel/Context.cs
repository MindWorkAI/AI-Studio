namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// Matching context returned by the data source as a result of a retrieval request.
/// </summary>
/// <param name="Name">The name of the source, e.g., a document name, database name,
/// collection name, etc.</param>
/// <param name="Category">What are the contents of the source? For example, is it a
/// dictionary, a book chapter, business concept, a paper, etc.</param>
/// <param name="Path">The path to the content, e.g., a URL, a file path, a path in a
/// graph database, etc.</param>
/// <param name="Type">The type of the content, e.g., text, image, video, audio, speech, etc.</param>
/// <param name="MatchedContent">The content that matched the user prompt. For text, you
/// return the matched text and, e.g., three words before and after it.</param>
/// <param name="SurroundingContent">The surrounding content of the matched content.
/// For text, you may return, e.g., one sentence or paragraph before and after
/// the matched content.</param>
/// <param name="Links">Links to related content, e.g., links to Wikipedia articles,
/// links to sources, etc.</param>
public readonly record struct Context(
    string Name,
    string Category,
    string? Path,
    ContentType Type,
    string MatchedContent,
    string[] SurroundingContent,
    string[] Links);