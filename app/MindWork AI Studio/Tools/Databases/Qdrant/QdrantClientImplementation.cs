using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AIStudio.Tools.Databases.Qdrant;

public class QdrantClientImplementation : DatabaseClient
{
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
    
    public QdrantClient CreateQdrantClient()
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

    public async Task<string> GetVersion()
    {
        var operation =  await this.GrpcClient.HealthAsync();
        return "v"+operation.Version;
    }

    public async Task<string> GetCollectionsAmount()
    {
        var operation = await this.GrpcClient.ListCollectionsAsync();
        return operation.Count.ToString();
    }
    
    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        yield return ("HTTP port", this.HttpPort.ToString());
        yield return ("gRPC port", this.GrpcPort.ToString());
        yield return ("Extracted version", await this.GetVersion());
        yield return ("Storage size", $"{base.GetStorageSize()}");
        yield return ("Amount of collections", await this.GetCollectionsAmount());
    }

    public override void Dispose()
    {
        this.GrpcClient.Dispose();
    }
}