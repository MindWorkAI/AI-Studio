using AIStudio.Tools.RAG;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

/// <summary>
/// One page of a search, as the model and the user receive it.
/// </summary>
/// <param name="TextContent">The passages the model reads, numbered, as Markdown.</param>
/// <param name="Sources">The sources of these passages, listed below the answer.</param>
/// <param name="Passages">What the user is shown of these passages.</param>
/// <param name="Outcomes">What the model learns about each data source searched, in the order they were searched.</param>
internal sealed record SemanticSearchResult(string TextContent, IReadOnlyList<Source> Sources, IReadOnlyList<SourcePassage> Passages, IReadOnlyList<DataSourceSearchOutcome> Outcomes);