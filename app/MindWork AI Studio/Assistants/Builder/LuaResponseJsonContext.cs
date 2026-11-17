using System.Text.Json.Serialization;

namespace AIStudio.Assistants.Builder;

/// <summary>
/// The contracts with which System.Text.Json reads the answer of a model which writes an assistant plugin, built at
/// compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(LuaResponse))]
internal sealed partial class LuaResponseJsonContext : JsonSerializerContext;