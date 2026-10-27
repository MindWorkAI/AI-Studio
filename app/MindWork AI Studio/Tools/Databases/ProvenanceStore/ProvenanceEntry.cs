namespace AIStudio.Tools.Databases.ProvenanceStore;

/// <summary>
/// One piece of content a chat received besides its messages, exactly as the AI received it.
/// </summary>
/// <remarks>
/// Where the content comes from, such as the file, the page, or the tool, is kept by the chat
/// itself, next to the id of this entry. The chat needs it to list its sources even without this
/// store, and it would only be stored twice here.
/// </remarks>
/// <param name="Id">The id the chat refers to the entry by.</param>
/// <param name="Kind">What sort of content this is.</param>
/// <param name="Content">The content, after the prompt injection filter.</param>
/// <param name="SurroundingContent">The content an ERI server sent along around its passage, empty for everything else.</param>
/// <param name="CreatedAtUtc">When the entry was stored.</param>
public sealed record ProvenanceEntry(Guid Id, ProvenanceKind Kind, string Content, IReadOnlyList<string> SurroundingContent, DateTimeOffset CreatedAtUtc);