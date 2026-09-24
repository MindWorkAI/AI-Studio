using System.Text.Json.Serialization;

using AIStudio.Chat;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG;

namespace AIStudio.Settings;

/// <summary>
/// The common interface for all data sources.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type_discriminator")]
[JsonDerivedType(typeof(DataSourceLocalDirectory), nameof(DataSourceType.LOCAL_DIRECTORY))]
[JsonDerivedType(typeof(DataSourceLocalFile), nameof(DataSourceType.LOCAL_FILE))]
[JsonDerivedType(typeof(DataSourceERI_V1), nameof(DataSourceType.ERI_V1))]
public interface IDataSource : IConfigurationObject
{
    /// <summary>
    /// Which type of data source is this?
    /// </summary>
    public DataSourceType Type { get; init; }

    /// <summary>
    /// The maximum number of matches one retrieval returns. Searched page by page, it is the size of a page.
    /// </summary>
    public ushort MaxMatches { get; init; }

    /// <summary>
    /// Perform the data retrieval process.
    /// </summary>
    /// <param name="lastUserPrompt">The last user prompt from the chat.</param>
    /// <param name="thread">The chat thread.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The retrieved data context.</returns>
    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IContent lastUserPrompt, ChatThread thread, CancellationToken token = default);

    /// <summary>
    /// Search the data source for a query of its own, one page at a time.
    /// </summary>
    /// <remarks>
    /// Unlike the retrieval above, the query need not be what the user wrote last: Semantic Search
    /// lets the model work it out from the conversation, and search as often as it takes. The first
    /// page holds what the retrieval above finds for the same text. How the pages are cut is
    /// described in RetrievalPaging.
    ///
    /// Since the user did not write the query, the user is not told about problems with it. They
    /// arrive in RetrievalPage.Gaps instead, together with everything else which kept the search
    /// from covering the whole data source.
    /// </remarks>
    /// <param name="query">What to search for.</param>
    /// <param name="page">The page to retrieve, from 1 up to RetrievalPaging.GetLastPage for MaxMatches.</param>
    /// <param name="thread">The chat thread.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The retrieved data contexts of this page, whether the next page is worth asking for, and what the search could not cover.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1 or beyond the last page.</exception>
    public Task<RetrievalPage> RetrieveDataAsync(string query, int page, ChatThread thread, CancellationToken token = default);
}