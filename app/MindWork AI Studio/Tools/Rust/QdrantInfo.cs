namespace AIStudio.Tools.Rust;

/// <summary>
/// The response of the Qdrant information request.
/// </summary>
/// <param name="portHTTP">The port number for HTTP communication with Qdrant.</param>
/// <param name="portGRPC">The port number for gRPC communication with Qdrant</param>
public record struct QdrantInfo
{
    public string Path { get; init; }
    public int PortHttp { get; init; }
    public int PortGrpc { get; init; }
}