using System.Text.Json.Serialization;

using ERIContentType = AIStudio.Tools.ERIClient.DataModel.ContentType;

namespace AIStudio.Chat;

/// <summary>
/// The contracts with which System.Text.Json reads and writes chats, built at compile time.
/// </summary>
/// <remarks>
/// Two types with the same name need a name of their own for their contract, else the generator builds
/// only the first of them and warns with SYSLIB1031.
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ChatThread))]
[JsonSerializable(typeof(ERIContentType), TypeInfoPropertyName = "ERIContentType")]
internal sealed partial class ChatJsonContext : JsonSerializerContext;