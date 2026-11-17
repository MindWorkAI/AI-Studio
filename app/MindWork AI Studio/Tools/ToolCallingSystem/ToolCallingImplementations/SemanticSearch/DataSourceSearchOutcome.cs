using AIStudio.Settings;
using AIStudio.Tools.RAG;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

/// <summary>
/// What a search tells the model about one data source, besides its passages.
/// </summary>
/// <param name="DataSource">The data source searched.</param>
/// <param name="ResultCount">How many of its passages reached the model.</param>
/// <param name="HasMore">Whether a further page of this data source holds passages.</param>
/// <param name="Gaps">What kept the search from covering the whole data source.</param>
/// <param name="LeftOutCount">How many of its passages a search of several data sources found, but had no room for.</param>
internal sealed record DataSourceSearchOutcome(IDataSource DataSource, int ResultCount, bool HasMore, IReadOnlyList<RetrievalGap> Gaps, int LeftOutCount = 0);