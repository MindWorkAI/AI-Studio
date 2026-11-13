using System.Text.Json.Serialization;

namespace AIStudio.Assistants.Builder;

/// <summary>
/// The contracts with which System.Text.Json reads the answer of a model which names a direct chat launcher, built
/// at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(LauncherTextsResponse))]
internal sealed partial class LauncherTextsResponseJsonContext : JsonSerializerContext;