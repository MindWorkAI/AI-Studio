namespace AIStudio.Provider;

public sealed record EmbeddingResponse
{
    public string? Id { get; set; }
    public string? Object { get; set; }
    public List<EmbeddingData>? Data { get; set; }
    public string? Model { get; set; }
    public Usage? Usage { get; set; }
}

public sealed record EmbeddingData
{
    public string? Object { get; set; }
    public List<float>? Embedding { get; set; }
    public int? Index { get; set; }
}

public sealed record Usage
{
    public int? PromptTokens { get; set; }
    public int? TotalTokens { get; set; }
    public int? CompletionTokens { get; set; }
}