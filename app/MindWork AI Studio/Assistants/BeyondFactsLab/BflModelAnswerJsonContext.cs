using System.Text.Json.Serialization;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The contracts with which System.Text.Json reads the JSON which models write for the BeyondFacts Lab, built at
/// compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(BflParticipantSelectionAnswer))]
internal sealed partial class BflModelAnswerJsonContext : JsonSerializerContext;