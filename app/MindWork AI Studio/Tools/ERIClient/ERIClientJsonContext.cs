using System.Text.Json.Serialization;

using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.ERIClient;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the calls to ERI servers, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]

// The request:
[JsonSerializable(typeof(RetrievalRequest))]

// The answers:
[JsonSerializable(typeof(List<AuthScheme>))]
[JsonSerializable(typeof(AuthResponse))]
[JsonSerializable(typeof(DataSourceInfo))]
[JsonSerializable(typeof(List<EmbeddingInfo>))]
[JsonSerializable(typeof(List<RetrievalInfo>))]
[JsonSerializable(typeof(List<Context>))]
[JsonSerializable(typeof(SecurityRequirements))]
internal sealed partial class ERIClientJsonContext : JsonSerializerContext;