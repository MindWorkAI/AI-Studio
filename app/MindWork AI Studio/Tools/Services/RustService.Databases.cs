using AIStudio.Tools.Databases;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<EmbeddingStoreConfiguration> GetEmbeddingStoreConfiguration(EmbeddingStoreKind kind)
    {
        switch (kind)
        {
            case EmbeddingStoreKind.QDRANT_REMOTE:
            {
                var qdrantInfo = await this.GetQdrantInfo();
                var invalidFields = new List<string>();
                if (!qdrantInfo.IsAvailable)
                    invalidFields.Add(qdrantInfo.UnavailableReason ?? "unknown");
                if (string.IsNullOrWhiteSpace(qdrantInfo.Path))
                    invalidFields.Add("Path");
                if (qdrantInfo.PortHttp == 0)
                    invalidFields.Add("HttpPort");
                if (qdrantInfo.PortGrpc == 0)
                    invalidFields.Add("GrpcPort");
                if (string.IsNullOrWhiteSpace(qdrantInfo.Fingerprint))
                    invalidFields.Add("Fingerprint");
                if (string.IsNullOrWhiteSpace(qdrantInfo.ApiToken))
                    invalidFields.Add("ApiToken");
                if (invalidFields.Count <= 0) return new EmbeddingStoreConfiguration(kind, "Qdrant", new RemoteLocation(qdrantInfo.Path, qdrantInfo.PortHttp, qdrantInfo.PortGrpc, qdrantInfo.Fingerprint, qdrantInfo.ApiToken), null);
                var reason = string.Join(", ", invalidFields);
                Console.WriteLine($"Warning: Qdrant is not available. Starting without vector database. Reason: '{reason}'.");

                return new EmbeddingStoreConfiguration(
                    EmbeddingStoreKind.NONE,
                    "Qdrant",
                    null,
                    reason);

            }
            default:
                return new EmbeddingStoreConfiguration(kind, kind.ToString(), null, $"No configuration available for {kind}");
        }
    }
    
    public async Task<QdrantInfo> GetQdrantInfo()
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var response = await this.http.GetFromJsonAsync<QdrantInfo>("/system/qdrant/info", this.jsonRustSerializerOptions, cts.Token);
            return response;
        }
        catch (Exception e)
        {
            if(this.logger is not null)
                this.logger.LogError(e, "Error while fetching Qdrant info from Rust service.");
            else
                Console.WriteLine($"Error while fetching Qdrant info from Rust service: '{e}'.");
            
            return default;
        }
    }
}