using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Databases;

public sealed partial class DatabaseClientProvider
{
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
}