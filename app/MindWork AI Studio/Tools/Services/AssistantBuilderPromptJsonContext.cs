using System.Text.Json.Serialization;

namespace AIStudio.Tools.Services;

/// <summary>
/// The contracts with which System.Text.Json writes what the Assistant Builder hands to its models, built at compile
/// time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AssistantBuilderDraftPromptData))]
[JsonSerializable(typeof(AssistantBuilderGenerationPromptData))]
[JsonSerializable(typeof(AssistantBuilderLauncherPromptData))]
[JsonSerializable(typeof(AssistantBuilderRevisionPromptData))]
internal sealed partial class AssistantBuilderPromptJsonContext : JsonSerializerContext;