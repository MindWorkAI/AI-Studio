namespace AIStudio.Tools.Databases.Qdrant;

public class QdrantClient(string name, string path, int httpPort, int grpcPort) : DatabaseClient(name, path)
{
    private int HttpPort { get; } = httpPort;
    private int GrpcPort { get; } = grpcPort;
    private string IpAddress { get; } = "127.0.0.1";
    
    public override IEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        yield return ("HTTP Port", this.HttpPort.ToString());
        yield return ("gRPC Port", this.GrpcPort.ToString());
        yield return ("Storage Size", $"{base.GetStorageSize()}");
    }
}