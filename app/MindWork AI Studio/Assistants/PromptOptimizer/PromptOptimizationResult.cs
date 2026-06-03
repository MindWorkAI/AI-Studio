using System.Text.Json.Serialization;

namespace AIStudio.Assistants.PromptOptimizer;

public sealed class PromptOptimizationResult
{
    [JsonPropertyName("optimized_prompt")]
    public string OptimizedPrompt { get; set; } = string.Empty;

    [JsonPropertyName("recommendations")]
    public PromptOptimizationRecommendations Recommendations { get; set; } = new();
}

public sealed class PromptOptimizationRecommendations
{
    [JsonPropertyName("clarity_and_directness")]
    public string ClarityAndDirectness { get; set; } = string.Empty;

    [JsonPropertyName("examples_and_context")]
    public string ExamplesAndContext { get; set; } = string.Empty;

    [JsonPropertyName("sequential_steps")]
    public string SequentialSteps { get; set; } = string.Empty;

    [JsonPropertyName("structure_with_markers")]
    public string StructureWithMarkers { get; set; } = string.Empty;

    [JsonPropertyName("role_definition")]
    public string RoleDefinition { get; set; } = string.Empty;

    [JsonPropertyName("language_choice")]
    public string LanguageChoice { get; set; } = string.Empty;
}
