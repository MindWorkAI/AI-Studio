using System.Text.Json.Serialization;

namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// The contracts with which System.Text.Json reads the slides a model plans, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SlideDeckSpec.DeckDto))]
internal sealed partial class SlideDeckJsonContext : JsonSerializerContext;