using AIStudio.Tools.Databases.Qdrant;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Databases;

public sealed class EmbeddingStoreProvider(RustService rustService, ILoggerFactory loggerFactory) : IDisposable
{
    private readonly Dictionary<DatabaseRole, EmbeddingStore> clients = new();
    private readonly Dictionary<DatabaseRole, SemaphoreSlim> locks = new();
    private readonly Lock locksLock = new();
    private readonly ILogger<EmbeddingStoreProvider> logger = loggerFactory.CreateLogger<EmbeddingStoreProvider>();
    private readonly ILogger<DatabaseClient> databaseClientLogger = loggerFactory.CreateLogger<DatabaseClient>();

    public async Task<DatabaseClient> GetClientAsync(DatabaseRole databaseRole, CancellationToken cancellationToken = default)
    {
        var databaseLock = this.GetLock(databaseRole);
        await databaseLock.WaitAsync(cancellationToken);
        try
        {
            if (this.clients.TryGetValue(databaseRole, out var cachedClient) && cachedClient.IsAvailable)
                return cachedClient;

            var client = await this.CreateClientAsync(databaseRole, cancellationToken);
            return this.CacheIfAvailable(databaseRole, client);
        }
        finally
        {
            databaseLock.Release();
        }
    }

    public async Task<DatabaseClient> RefreshClientAsync(DatabaseRole databaseRole, CancellationToken cancellationToken = default)
    {
        var databaseLock = this.GetLock(databaseRole);
        await databaseLock.WaitAsync(cancellationToken);
        try
        {
            var client = await this.CreateClientAsync(databaseRole, cancellationToken);
            return this.CacheIfAvailable(databaseRole, client);
        }
        finally
        {
            databaseLock.Release();
        }
    }

    private DatabaseClient CacheIfAvailable(DatabaseRole databaseRole, DatabaseClient client)
    {
        if (!client.IsAvailable)
            return client;

        if (this.clients.TryGetValue(databaseRole, out var cachedClient))
        {
            if (IsSameClient(cachedClient, client))
            {
                client.Dispose();
                return cachedClient;
            }

            cachedClient.Dispose();
        }

        this.clients[databaseRole] = client;
        return client;
    }

    private SemaphoreSlim GetLock(DatabaseRole databaseRole)
    {
        lock (this.locksLock)
        {
            if (this.locks.TryGetValue(databaseRole, out var databaseLock))
                return databaseLock;

            databaseLock = new SemaphoreSlim(1, 1);
            this.locks[databaseRole] = databaseLock;
            return databaseLock;
        }
    }

    private async Task<DatabaseClient> CreateClientAsync(DatabaseRole databaseRole, CancellationToken cancellationToken) => databaseRole switch
    {
        DatabaseRole.VECTOR_STORE => await this.CreateQdrantClientAsync(cancellationToken),
        _ => new NoDatabaseClient(databaseRole.ToString(), "The requested database role is not supported.")
    };

    private async Task<DatabaseClient> CreateQdrantClientAsync(CancellationToken cancellationToken)
    {
        var qdrantInfo = await rustService.GetQdrantInfo(cancellationToken);
        if (qdrantInfo.Status is QdrantStatus.STARTING)
        {
            return this.CreateNoDatabaseClient(
                "Qdrant",
                "Qdrant is starting. Details will appear shortly.",
                DatabaseClientStatus.STARTING);
        }

        if (!qdrantInfo.IsAvailable || qdrantInfo.Status is QdrantStatus.UNAVAILABLE)
        {
            var reason = qdrantInfo.UnavailableReason ?? "unknown";
            this.logger.LogWarning("Qdrant is not available. Starting without vector database. Reason: '{Reason}'.", reason);
            return this.CreateNoDatabaseClient("Qdrant", qdrantInfo.UnavailableReason, DatabaseClientStatus.UNAVAILABLE);
        }

        if (!HasValidQdrantConnectionInfo(qdrantInfo, out var invalidReason))
            return this.CreateNoDatabaseClient("Qdrant", invalidReason, DatabaseClientStatus.UNAVAILABLE);

        var client = new QdrantClientImplementation("Qdrant", qdrantInfo.Path, qdrantInfo.PortHttp, qdrantInfo.PortGrpc, qdrantInfo.Fingerprint, qdrantInfo.ApiToken);
        client.SetLogger(this.databaseClientLogger);

        try
        {
            await client.CheckAvailabilityAsync();
            return client;
        }
        catch (Exception e)
        {
            client.Dispose();
            this.logger.LogWarning(e, "Qdrant reported as available by Rust, but the health check failed.");
            return this.CreateNoDatabaseClient("Qdrant", e.Message, DatabaseClientStatus.STARTING);
        }
    }

    private static bool HasValidQdrantConnectionInfo(QdrantInfo qdrantInfo, out string invalidReason)
    {
        if (qdrantInfo.Path == string.Empty)
        {
            invalidReason = "Failed to get the Qdrant path from Rust.";
            return false;
        }

        if (qdrantInfo.PortHttp == 0)
        {
            invalidReason = "Failed to get the Qdrant HTTP port from Rust.";
            return false;
        }

        if (qdrantInfo.PortGrpc == 0)
        {
            invalidReason = "Failed to get the Qdrant gRPC port from Rust.";
            return false;
        }

        if (qdrantInfo.Fingerprint == string.Empty)
        {
            invalidReason = "Failed to get the Qdrant fingerprint from Rust.";
            return false;
        }

        if (qdrantInfo.ApiToken == string.Empty)
        {
            invalidReason = "Failed to get the Qdrant API token from Rust.";
            return false;
        }

        invalidReason = string.Empty;
        return true;
    }

    private NoDatabaseClient CreateNoDatabaseClient(string name, string? unavailableReason, DatabaseClientStatus status)
    {
        var client = new NoDatabaseClient(name, unavailableReason, status);
        client.SetLogger(this.databaseClientLogger);
        return client;
    }

    private static bool IsSameClient(DatabaseClient left, DatabaseClient right) =>
        left.IsAvailable
        && right.IsAvailable
        && left.CacheKey == right.CacheKey;

    public void Dispose()
    {
        foreach (var client in this.clients.Values)
            client.Dispose();

        foreach (var databaseLock in this.locks.Values)
            databaseLock.Dispose();
    }
}