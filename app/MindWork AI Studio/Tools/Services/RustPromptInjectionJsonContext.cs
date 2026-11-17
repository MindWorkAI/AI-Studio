using System.Text.Json.Serialization;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the calls to the prompt injection filter of the
/// runtime, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SanitizePromptInjectionsRequest))]
[JsonSerializable(typeof(SanitizePromptInjectionsResponse))]
[JsonSerializable(typeof(SanitizePromptInjectionsBatchRequest))]
[JsonSerializable(typeof(SanitizePromptInjectionsBatchResponse))]
internal sealed partial class RustPromptInjectionJsonContext : JsonSerializerContext;