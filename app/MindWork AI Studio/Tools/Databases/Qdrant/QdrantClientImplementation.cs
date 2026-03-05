using Qdrant.Client;
using Qdrant.Client.Grpc;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Databases.Qdrant;

public class QdrantClientImplementation : DatabaseClient
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(QdrantClientImplementation).Namespace, nameof(QdrantClientImplementation));

    private int HttpPort { get; }
    
    private int GrpcPort { get; }
    
    private QdrantClient GrpcClient { get; }
    
    private string Fingerprint { get; }
    
    private string ApiToken { get; }
    
    public QdrantClientImplementation(string name, string path, int httpPort, int grpcPort, string fingerprint, string apiToken): base(name, path)
    {
        this.HttpPort = httpPort;
        this.GrpcPort = grpcPort;
        this.Fingerprint = fingerprint;
        this.ApiToken = apiToken;
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
        var operation =  await this.GrpcClient.HealthAsync();
        return "v"+operation.Version;
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

    public override void Dispose() => this.GrpcClient.Dispose();
}