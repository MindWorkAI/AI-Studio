namespace AIStudio.Tools.Rust;

/// <summary>
/// The response of the Qdrant information request.
/// </summary>
public record struct QdrantInfo
{
    public string Path { get; init; }
    
    public int PortHttp { get; init; }
    
    public int PortGrpc { get; init; }
    
    public string Fingerprint { get; init; }
    
    public string ApiToken { get; init; }
}