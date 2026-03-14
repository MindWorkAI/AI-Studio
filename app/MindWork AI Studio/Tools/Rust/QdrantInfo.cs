namespace AIStudio.Tools.Rust;

/// <summary>
/// The response of the Qdrant information request.
/// </summary>
public readonly record struct QdrantInfo
{
    public bool IsAvailable { get; init; }

    public string? UnavailableReason { get; init; }

    public string Path { get; init; }
    
    public int PortHttp { get; init; }
    
    public int PortGrpc { get; init; }
    
    public string Fingerprint { get; init; }
    
    public string ApiToken { get; init; }
}