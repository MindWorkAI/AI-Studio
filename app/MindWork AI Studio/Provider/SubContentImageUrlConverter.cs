using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider;

/// <summary>
/// Custom JSON converter for the ISubContentImageUrl interface to handle polymorphic serialization.
/// </summary>
/// <remarks>
/// This converter ensures that when serializing ISubContentImageUrl objects, all properties
/// of the concrete implementation (e.g., SubContentImageUrlData) are serialized,
/// not just the properties defined in the ISubContentImageUrl interface.
/// </remarks>
public sealed class SubContentImageUrlConverter : JsonConverter<ISubContentImageUrl>
{
    private static readonly ILogger<SubContentImageUrlConverter> LOGGER = Program.LOGGER_FACTORY.CreateLogger<SubContentImageUrlConverter>();

    public override ISubContentImageUrl? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Deserialization is not needed for request objects, as sub-content image URLs are only serialized
        // when sending requests to LLM providers.
        LOGGER.LogError("Deserializing ISubContentImageUrl is not supported. This converter is only used for serializing request messages.");
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ISubContentImageUrl value, JsonSerializerOptions options)
    {
        // Serialize the actual concrete type (e.g., SubContentImageUrlData) instead of just the ISubContentImageUrl interface.
        // This ensures all properties of the concrete type are included in the JSON output.
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
