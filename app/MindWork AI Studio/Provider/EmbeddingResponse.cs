namespace AIStudio.Provider;

public sealed record EmbeddingResponse
{
    public string? Id { get; set; }
    
    public string? Object { get; set; }
    
    public List<EmbeddingData>? Data { get; set; }
    
    public string? Model { get; set; }
    
    public EmbeddingUsage? Usage { get; set; }
}