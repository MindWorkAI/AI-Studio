using AIStudio.Settings;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

/// <summary>
/// A search as the model asked for it, checked against the data sources offered.
/// </summary>
/// <param name="Query">What to search for.</param>
/// <param name="DataSources">The data sources to search, in the order they are offered.</param>
/// <param name="Page">The page to retrieve from each of them, starting at 1.</param>
internal sealed record SemanticSearchRequest(string Query, IReadOnlyList<IDataSource> DataSources, int Page);