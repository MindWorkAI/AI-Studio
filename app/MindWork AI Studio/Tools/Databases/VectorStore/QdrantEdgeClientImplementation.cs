using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Databases.VectorStore;

public sealed class QdrantEdgeClientImplementation(
    string name,
    string path,
    string version,
    int storesCount,
    RustService rustService) : DatabaseClient(name, path), IVectorStoreClient
{
    private const string DATABASE_NAME = "Qdrant Edge";
    private const string INFO_PATH = "/system/qdrant-edge/info";
    private const string ENSURE_PATH = "/system/qdrant-edge/ensure";
    private const string INSERT_PATH = "/system/qdrant-edge/insert";
    private const string DELETE_FILE_PATH = "/system/qdrant-edge/delete-file";
    private const string DELETE_STORE_PATH = "/system/qdrant-edge/delete-store";
    
    private readonly string path = path;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(QdrantEdgeClientImplementation).Namespace, nameof(QdrantEdgeClientImplementation));

    public override string CacheKey => $"{this.Name}:{this.path}:{version}";

    public static async Task<DatabaseClient> CreateAsync(
        RustService rustService,
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
        var client = new QdrantEdgeClientImplementation(name, qdrantEdgeInfo.Path, qdrantEdgeInfo.Version, qdrantEdgeInfo.StoresCount, rustService);
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

        yield return (TB("Reported version"), displayVersion);
        yield return (TB("Storage size"), $"{this.GetStorageSize()}");
        yield return (TB("Number of vector stores"), displayStoresCount.ToString());
    }

    public Task EnsureVectorStoreExists(string storeName, int vectorSize, CancellationToken token) =>
        rustService.ExecuteDatabaseOperation(DATABASE_NAME, ENSURE_PATH, new EnsureVectorStoreRequest(storeName, vectorSize), token);

    public Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token) =>
        rustService.ExecuteDatabaseOperation(DATABASE_NAME, INSERT_PATH, new InsertEmbeddingRequest(storeName, points), token);

    public Task DeleteEmbeddingByFile(string storeName, string filePath, CancellationToken token) =>
        rustService.ExecuteDatabaseOperation(DATABASE_NAME, DELETE_FILE_PATH, new DeleteEmbeddingByFileRequest(storeName, filePath), token);

    public Task DeleteVectorStore(string storeName, CancellationToken token) =>
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
    private sealed record EnsureVectorStoreRequest(string StoreName, int VectorSize);

    private sealed record InsertEmbeddingRequest(string StoreName, IReadOnlyList<VectorStoragePoint> Points);

    private sealed record DeleteEmbeddingByFileRequest(string StoreName, string FilePath);
    
    private sealed record DeleteVectorStoreRequest(string StoreName);
    // ReSharper restore NotAccessedPositionalProperty.Local
}