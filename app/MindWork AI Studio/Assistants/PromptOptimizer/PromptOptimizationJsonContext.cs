using System.Text.Json.Serialization;

namespace AIStudio.Assistants.PromptOptimizer;

/// <summary>
/// The contracts with which System.Text.Json reads the answer of the prompt optimizer, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PromptOptimizationResult))]
internal sealed partial class PromptOptimizationJsonContext : JsonSerializerContext;