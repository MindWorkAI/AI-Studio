using AIStudio.Tools.Services;
using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tools.Databases;

public sealed partial class DatabaseClientProvider(RustService rustService, ILoggerFactory loggerFactory) : IDisposable
{
    private readonly Dictionary<DatabaseRole, DatabaseClient> clients = new();
    private readonly Dictionary<DatabaseRole, SemaphoreSlim> locks = new();
    private readonly Lock locksLock = new();
    private readonly ILogger<DatabaseClientProvider> logger = loggerFactory.CreateLogger<DatabaseClientProvider>();
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

    public async Task<VectorStoreClient> GetVectorStoreAsync(CancellationToken cancellationToken = default)
    {
        var client = await this.GetClientAsync(DatabaseRole.VECTOR_STORE, cancellationToken);
        if (client is VectorStoreClient vectorStore)
            return vectorStore;

        return new NoVectorStoreClient(
            client.Name,
            "The configured database client does not support vector store operations.",
            client.Status);
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
        DatabaseRole.VECTOR_STORE => await QdrantEdgeClientImplementation.CreateAsync(rustService, this.logger, this.databaseClientLogger, cancellationToken),
        _ => new NoDatabaseClient(databaseRole.ToString(), "The requested database role is not supported.")
    };

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