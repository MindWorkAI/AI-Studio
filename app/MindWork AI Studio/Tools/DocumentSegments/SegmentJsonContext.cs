using System.Text.Json.Serialization;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// The contracts with which System.Text.Json writes the segments for a model and reads its answer, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SegmentBatch))]
internal sealed partial class SegmentJsonContext : JsonSerializerContext;