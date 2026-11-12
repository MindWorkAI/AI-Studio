using System.Text.Json.Serialization;

namespace AIStudio.Tools.Feedback;

/// <summary>
/// The contract with which System.Text.Json writes the weekly feedback for the feedback server, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(FeedbackPayload))]
internal sealed partial class FeedbackJsonContext : JsonSerializerContext;