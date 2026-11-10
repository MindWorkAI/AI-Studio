using System.Text.Json.Serialization;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the files of the BeyondFacts Lab, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(BflProject))]
[JsonSerializable(typeof(BflStepFormData))]
[JsonSerializable(typeof(BflMaterial))]
[JsonSerializable(typeof(BflWorkState))]
[JsonSerializable(typeof(BflResult))]
[JsonSerializable(typeof(BflResearch))]

// The preparation selected last:
[JsonSerializable(typeof(Guid?))]
internal sealed partial class BeyondFactsLabJsonContext : JsonSerializerContext;