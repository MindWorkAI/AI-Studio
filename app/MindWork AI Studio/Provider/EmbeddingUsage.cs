// ReSharper disable ClassNeverInstantiated.Global
namespace AIStudio.Provider;

public sealed record EmbeddingUsage
{
    public int? PromptTokens { get; set; }
    
    public int? TotalTokens { get; set; }
    
    public int? CompletionTokens { get; set; }
}