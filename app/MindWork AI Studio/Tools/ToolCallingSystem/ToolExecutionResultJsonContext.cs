using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// The contracts of the options with which a tool result is written for the model, built at compile time.
/// </summary>
/// <remarks>
/// A JSON result is a tree of JSON nodes, which writes itself and needs no contract from the options. The
/// context is there all the same, so that these options can never fall back to reflection either.
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(JsonNode))]
internal sealed partial class ToolExecutionResultJsonContext : JsonSerializerContext;