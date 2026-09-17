using System.Globalization;

using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Databases.VectorStore;

/// <param name="indexStoreAccessor">
/// Resolves the index store, which is where the number of stored vectors comes from. Counting the
/// points in Qdrant Edge itself would have to load every shard first and would hold the global
/// database mutex against ongoing inserts and searches. The accessor is only called while building
/// the display info, never while this client is created: creating it already holds the vector store
/// lock, and the accessor takes the index store lock, so the two are never held at the same time.
/// </param>
public sealed class QdrantEdgeClientImplementation(
    string name,
    string path,
    string version,
    int storesCount,
    RustService rustService,
    Func<CancellationToken, Task<IndexStoreClient>> indexStoreAccessor) : VectorStoreClient(name, path)
{
    private const string DATABASE_NAME = "Qdrant Edge";
    private const string INFO_PATH = "/system/qdrant-edge/info";
    private const string ENSURE_PATH = "/system/qdrant-edge/ensure";
    private const string INSERT_PATH = "/system/qdrant-edge/insert";
    private const string SEARCH_PATH = "/system/qdrant-edge/search";
    private const string DELETE_FILE_PATH = "/system/qdrant-edge/delete-file";
    private const string OPTIMIZE_PATH = "/system/qdrant-edge/optimize";
    private const string DELETE_STORE_PATH = "/system/qdrant-edge/delete-store";
    
    private readonly string path = path;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(QdrantEdgeClientImplementation).Namespace, nameof(QdrantEdgeClientImplementation));

    public override string CacheKey => $"{this.Name}:{this.path}:{version}";

    public static async Task<DatabaseClient> CreateAsync(
        RustService rustService,
        Func<CancellationToken, Task<IndexStoreClient>> indexStoreAccessor,
        ILogger logger,
        ILogger<DatabaseClient> databaseClientLogger,
        CancellationToken cancellationToken)
    {
        var qdrantEdgeInfo = await rustService.GetDatabaseInfo(
            DATABASE_NAME,
            INFO_PATH,
            QdrantEdgeInfo.Unavailable,
            cancellationToken);

        if (qdrantEdgeInfo.Status is QdrantEdgeStatus.STARTING)
        {
            return CreateNoVectorStoreClient(
                DATABASE_NAME,
                $"{DATABASE_NAME} is starting. Details will appear shortly.",
                DatabaseClientStatus.STARTING,
                databaseClientLogger);
        }

        if (!qdrantEdgeInfo.IsAvailable || qdrantEdgeInfo.Status is QdrantEdgeStatus.UNAVAILABLE)
        {
            var reason = qdrantEdgeInfo.UnavailableReason ?? "unknown";
            // ReSharper disable DuplicateItemInLoggerTemplate
            logger.LogWarning("{VectorStoreName} is not available. Starting without {VectorStoreName} vector store. Reason: '{Reason}'.", DATABASE_NAME, DATABASE_NAME, reason);
            // ReSharper restore DuplicateItemInLoggerTemplate
            return CreateNoVectorStoreClient(DATABASE_NAME, qdrantEdgeInfo.UnavailableReason, DatabaseClientStatus.UNAVAILABLE, databaseClientLogger);
        }

        if (qdrantEdgeInfo.Path == string.Empty)
            return CreateNoVectorStoreClient(DATABASE_NAME, $"Failed to get the {DATABASE_NAME} path from Rust.", DatabaseClientStatus.UNAVAILABLE, databaseClientLogger);

        var name = string.IsNullOrWhiteSpace(qdrantEdgeInfo.Name) ? DATABASE_NAME : qdrantEdgeInfo.Name;
        var client = new QdrantEdgeClientImplementation(name, qdrantEdgeInfo.Path, qdrantEdgeInfo.Version, qdrantEdgeInfo.StoresCount, rustService, indexStoreAccessor);
        client.SetLogger(databaseClientLogger);
        return client;
    }

    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        var currentInfo = await rustService.GetDatabaseInfo(
            DATABASE_NAME,
            INFO_PATH,
            QdrantEdgeInfo.Unavailable);
        var displayVersion = currentInfo.IsAvailable && !string.IsNullOrWhiteSpace(currentInfo.Version) ? currentInfo.Version : version;
        var displayStoresCount = currentInfo.IsAvailable ? currentInfo.StoresCount : storesCount;

        if (!currentInfo.IsAvailable)
            yield return (TB("Status"), currentInfo.UnavailableReason ?? TB("Qdrant Edge is not available."));

        var storedVectors = await this.GetStoredVectorCountAsync();

        yield return (TB("Reported version"), displayVersion);
        yield return (TB("Storage size"), $"{this.GetStorageSize()}");
        yield return (TB("Number of vector stores"), displayStoresCount.ToString("N0", I18N.I.Culture));
        yield return (TB("Stored vectors"), storedVectors?.ToString("N0", I18N.I.Culture) ?? TB("unknown"));
    }

    /// <summary>
    /// Reads how many vectors the vector stores hold in total.
    /// </summary>
    /// <returns>The number of vectors, or null when the index store cannot tell.</returns>
    private async Task<long?> GetStoredVectorCountAsync()
    {
        try
        {
            //
            // One chunk is one vector: every chunk becomes exactly one point carrying the single
            // named vector "embedding". So the index store knows this number without Qdrant Edge
            // having to load a single shard for it.
            //
            var indexStore = await indexStoreAccessor(CancellationToken.None);
            return await indexStore.GetTotalChunkCountAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            this.Logger?.LogWarning(exception, "Failed to read the number of stored vectors from the index store.");
            return null;
        }
    }

    public override async Task<VectorStoreEnsureResult> EnsureVectorStoreExists(string storeName, string dataSourceName, int vectorSize, CancellationToken token) =>
        await rustService.ExecuteDatabaseQuery<EnsureVectorStoreRequest, VectorStoreEnsureResult>(DATABASE_NAME, ENSURE_PATH,
            new EnsureVectorStoreRequest(storeName, dataSourceName, vectorSize), token) ?? throw new InvalidOperationException("The vector store ensure response was empty.");

    public override Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token) =>
        rustService.ExecuteDatabaseOperation(DATABASE_NAME, INSERT_PATH, new InsertEmbeddingRequest(storeName, points), token);

    public override async Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, CancellationToken token)
    {
        if (maxMatches <= 0)
            return [];

        return await rustService.ExecuteDatabaseQuery<SearchEmbeddingRequest, List<VectorSearchResult>>(
            DATABASE_NAME,
            SEARCH_PATH,
            new SearchEmbeddingRequest(storeName, vector, maxMatches),
            token) ?? [];
    }

    public override Task DeleteEmbeddingByFile(string storeName, string filePath, CancellationToken token) =>
        rustService.ExecuteDatabaseOperation(DATABASE_NAME, DELETE_FILE_PATH, new DeleteEmbeddingByFileRequest(storeName, filePath), token);

    public override Task OptimizeVectorStore(string storeName, CancellationToken token) =>
        rustService.ExecuteDatabaseOperation(DATABASE_NAME, OPTIMIZE_PATH, new OptimizeVectorStoreRequest(storeName), token);

    public override Task DeleteVectorStore(string storeName, CancellationToken token) =>
        rustService.ExecuteDatabaseOperation(DATABASE_NAME, DELETE_STORE_PATH, new DeleteVectorStoreRequest(storeName), token);

    public override void Dispose()
    {
    }

    private static NoVectorStoreClient CreateNoVectorStoreClient(string name, string? unavailableReason, DatabaseClientStatus status, ILogger<DatabaseClient> databaseClientLogger)
    {
        var client = new NoVectorStoreClient(name, unavailableReason, status);
        client.SetLogger(databaseClientLogger);
        return client;
    }

    // ReSharper disable NotAccessedPositionalProperty.Local
    private sealed record EnsureVectorStoreRequest(string StoreName, string DataSourceName, int VectorSize);

    private sealed record InsertEmbeddingRequest(string StoreName, IReadOnlyList<VectorStoragePoint> Points);

    private sealed record SearchEmbeddingRequest(string StoreName, IReadOnlyList<float> Vector, int MaxMatches);

    private sealed record DeleteEmbeddingByFileRequest(string StoreName, string FilePath);

    private sealed record OptimizeVectorStoreRequest(string StoreName);
    
    private sealed record DeleteVectorStoreRequest(string StoreName);
    // ReSharper restore NotAccessedPositionalProperty.Local
}