namespace AIStudio.Provider;

public sealed record EmbeddingResponse
{
    public string? Id { get; init; }
    
    public string? Object { get; init; }
    
    public List<EmbeddingData>? Data { get; init; }
    
    public string? Model { get; init; }
    
    public EmbeddingUsage? Usage { get; init; }
}