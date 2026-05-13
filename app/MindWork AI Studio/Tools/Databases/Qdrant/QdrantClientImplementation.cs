using Qdrant.Client;
using Qdrant.Client.Grpc;
using Grpc.Core;
using AIStudio.Tools.PluginSystem;
using static Qdrant.Client.Grpc.Conditions;

namespace AIStudio.Tools.Databases.Qdrant;

public class QdrantClientImplementation : EmbeddingStore
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(QdrantClientImplementation).Namespace, nameof(QdrantClientImplementation));

    private int HttpPort { get; }
    
    private int GrpcPort { get; }
    
    private QdrantClient GrpcClient { get; }
    
    private string Fingerprint { get; }
    
    private string ApiToken { get; }
    
    public QdrantClientImplementation(string name, string path, int? httpPort, int? grpcPort, string? fingerprint, string? apiToken): base(name, path)
    {
        this.HttpPort = httpPort ?? 0;
        this.GrpcPort = grpcPort ?? 0;
        this.Fingerprint = fingerprint ?? string.Empty;
        this.ApiToken = apiToken ?? string.Empty;
        this.GrpcClient = this.CreateQdrantClient();
    }
    
    private const string IP_ADDRESS = "localhost";

    private QdrantClient CreateQdrantClient()
    {
        var address = "https://" + IP_ADDRESS + ":" + this.GrpcPort;
        var channel = QdrantChannel.ForAddress(address, new ClientConfiguration
        {
            ApiKey = this.ApiToken,
            CertificateThumbprint = this.Fingerprint
        });
        var grpcClient = new QdrantGrpcClient(channel);
        return new QdrantClient(grpcClient);
    }

    private async Task<string> GetVersion()
    {
        var operation = await this.GrpcClient.HealthAsync();
        return $"v{operation.Version}";
    }

    private async Task<string> GetCollectionsAmount()
    {
        var operation = await this.GrpcClient.ListCollectionsAsync();
        return operation.Count.ToString();
    }
    
    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        yield return (TB("HTTP port"), this.HttpPort.ToString());
        yield return (TB("gRPC port"), this.GrpcPort.ToString());
        yield return (TB("Reported version"), await this.GetVersion());
        yield return (TB("Storage size"), $"{this.GetStorageSize()}");
        yield return (TB("Number of collections"), await this.GetCollectionsAmount());
    }

    public override async Task EnsureEmbeddingStoreExists(string collectionName, int vectorSize, CancellationToken token)
    {
        var exists = await this.GrpcClient.CollectionExistsAsync(collectionName, token);
        if (exists)
            return;

        await this.GrpcClient.CreateCollectionAsync(
            collectionName,
            new VectorParams
            {
                Size = (ulong)vectorSize,
                Distance = Distance.Cosine,
            },
            cancellationToken: token);
    }

    public override Task InsertEmbedding(string collectionName, IReadOnlyList<EmbeddingStoragePoint> points, CancellationToken token)
    {
        var qdrantPoints = points.Select(point => new PointStruct
        {
            Id = Guid.Parse(point.PointId),
            Vectors = point.Vector.ToArray(),
            Payload =
            {
                ["data_source_id"] = point.DataSourceId,
                ["data_source_name"] = point.DataSourceName,
                ["data_source_type"] = point.DataSourceType,
                ["file_path"] = point.FilePath,
                ["file_name"] = point.FileName,
                ["relative_path"] = point.RelativePath,
                ["chunk_index"] = (long)point.ChunkIndex,
                ["text"] = point.Text,
                ["fingerprint"] = point.Fingerprint,
                ["last_write_utc"] = point.LastWriteUtc.ToString("O"),
                ["embedded_at_utc"] = point.EmbeddedAtUtc.ToString("O"),
            }
        }).ToList();

        return this.GrpcClient.UpsertAsync(collectionName, qdrantPoints, true, null, null, token);
    }

    public override async Task DeleteEmbeddingByFile(string collectionName, string filePath, CancellationToken token)
    {
        try
        {
            await this.GrpcClient.DeleteAsync(collectionName, MatchKeyword("file_path", filePath), true, null, null, token);
        }
        catch (RpcException exception) when (exception.StatusCode is StatusCode.NotFound)
        {
            return;
        }
    }

    public override async Task DeleteEmbeddingStore(string collectionName, CancellationToken token)
    {
        var exists = await this.GrpcClient.CollectionExistsAsync(collectionName, token);
        if (!exists)
            return;

        try
        {
            await this.GrpcClient.DeleteCollectionAsync(collectionName, cancellationToken: token);
        }
        catch (RpcException exception) when (exception.StatusCode is StatusCode.NotFound)
        {
            return;
        }
    }

    public override void Dispose() => this.GrpcClient.Dispose();
}
