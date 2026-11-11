using System.Text.Json.Serialization;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// The generated JSON contract for the stored entries of a glossary.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(GlossaryStore.GlossaryFile))]
internal sealed partial class GlossaryJsonContext : JsonSerializerContext;
