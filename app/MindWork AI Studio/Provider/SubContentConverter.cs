using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider;

/// <summary>
/// Custom JSON converter for the ISubContent interface to handle polymorphic serialization.
/// </summary>
/// <remarks>
/// This converter ensures that when serializing ISubContent objects, all properties
/// of the concrete implementation (e.g., SubContentText, SubContentImageUrl) are serialized,
/// not just the properties defined in the ISubContent interface.
/// </remarks>
public sealed class SubContentConverter : JsonConverter<ISubContent>
{
    private static readonly ILogger<SubContentConverter> LOGGER = Program.LOGGER_FACTORY.CreateLogger<SubContentConverter>();

    public override ISubContent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Deserialization is not needed for request objects, as sub-content is only serialized
        // when sending requests to LLM providers.
        LOGGER.LogError("Deserializing ISubContent is not supported. This converter is only used for serializing request messages.");
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ISubContent value, JsonSerializerOptions options)
    {
        // Serialize the actual concrete type (e.g., SubContentText, SubContentImageUrl) instead of just the ISubContent interface.
        // This ensures all properties of the concrete type are included in the JSON output.
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
