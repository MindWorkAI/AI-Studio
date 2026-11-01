using System.Text.Json.Serialization;

using AIStudio.Chat;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.RAG;

namespace AIStudio.Settings;

/// <summary>
/// The common interface for the data sources in DataSources, which classic RAG, Semantic Search and the agents read.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type_discriminator")]
[JsonDerivedType(typeof(DataSourceLocalDirectory), nameof(DataSourceType.LOCAL_DIRECTORY))]
[JsonDerivedType(typeof(DataSourceLocalFile), nameof(DataSourceType.LOCAL_FILE))]
[JsonDerivedType(typeof(DataSourceERI_V1), nameof(DataSourceType.ERI_V1))]
public interface IDataSource : IDataSourceBase
{
    /// <summary>
    /// The maximum number of matches one retrieval returns. Searched page by page, it is the size of a page.
    /// </summary>
    public ushort MaxMatches { get; set; }

    /// <summary>
    /// Where a chat may still send data, once content of this data source reached it.
    /// </summary>
    /// <remarks>
    /// Read it through DataSourceOutboundDataExtensions.GetEffectiveOutboundDataRestriction, never
    /// on its own: an organization may demand a stricter level, see
    /// DataSourceSecuritySettings.MinimumOutboundDataRestriction, and so does a data source which only
    /// self-hosted providers may see.
    /// </remarks>
    public OutboundDataRestriction OutboundDataRestriction { get; set; }

    /// <summary>
    /// The last page which can be retrieved from this data source.
    /// </summary>
    /// <remarks>
    /// By default, as many pages as RetrievalPaging.GetLastPage allows for the page size. A data
    /// source which cannot page that far says so itself. It is a method rather than a property, so
    /// that it never ends up in the settings.
    /// </remarks>
    /// <returns>The number of the last page, which is at least 1.</returns>
    public int GetLastPage() => RetrievalPaging.GetLastPage(this.MaxMatches);

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
    /// <param name="page">The page to retrieve, from 1 up to GetLastPage.</param>
    /// <param name="thread">The chat thread.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The retrieved data contexts of this page, whether the next page is worth asking for, and what the search could not cover.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1 or beyond the last page.</exception>
    public Task<RetrievalPage> RetrieveDataAsync(string query, int page, ChatThread thread, CancellationToken token = default);
}