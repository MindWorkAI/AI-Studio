using System.Text.Json.Serialization;

namespace AIStudio.Assistants.ERI;

/// <summary>
/// The contracts with which System.Text.Json reads the files a model plans for an ERI server, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ERIServerFileList))]
internal sealed partial class ERIServerFileListJsonContext : JsonSerializerContext;