using System.Text.Json.Serialization;

namespace AIStudio.Assistants.PromptOptimizer;

public sealed class PromptOptimizationResult
{
    [JsonPropertyName("optimized_prompt")]
    public string OptimizedPrompt { get; set; } = string.Empty;

    [JsonPropertyName("recommendations")]
    public PromptOptimizationRecommendations Recommendations { get; set; } = new();
}