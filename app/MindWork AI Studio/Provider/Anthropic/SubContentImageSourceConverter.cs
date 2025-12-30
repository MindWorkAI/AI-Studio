using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// Custom JSON converter for the ISubContentImageSource interface to handle polymorphic serialization.
/// </summary>
/// <remarks>
/// This converter ensures that when serializing ISubContentImageSource objects, all properties
/// of the concrete implementation (e.g., SubContentBase64Image, SubContentImageUrl) are serialized,
/// not just the properties defined in the ISubContentImageSource interface.
/// </remarks>
public sealed class SubContentImageSourceConverter : JsonConverter<ISubContentImageSource>
{
    private static readonly ILogger<SubContentImageSourceConverter> LOGGER = Program.LOGGER_FACTORY.CreateLogger<SubContentImageSourceConverter>();

    public override ISubContentImageSource? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Deserialization is not needed for request objects, as sub-content image sources are only serialized
        // when sending requests to LLM providers.
        LOGGER.LogError("Deserializing ISubContentImageSource is not supported. This converter is only used for serializing request messages.");
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ISubContentImageSource value, JsonSerializerOptions options)
    {
        // Serialize the actual concrete type (e.g., SubContentBase64Image, SubContentImageUrl) instead of just the ISubContentImageSource interface.
        // This ensures all properties of the concrete type are included in the JSON output.
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
