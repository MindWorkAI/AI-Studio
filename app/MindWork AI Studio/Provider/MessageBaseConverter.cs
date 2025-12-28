using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider;

/// <summary>
/// Custom JSON converter for the IMessageBase interface to handle polymorphic serialization.
/// </summary>
/// <remarks>
/// This converter ensures that when serializing IMessageBase objects, all properties
/// of the concrete implementation (e.g., TextMessage) are serialized, not just the
/// properties defined in the IMessageBase interface.
/// </remarks>
public sealed class MessageBaseConverter : JsonConverter<IMessageBase>
{
    public override IMessageBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Deserialization is not needed for request objects, as messages are only serialized
        // when sending requests to LLM providers.
        throw new NotImplementedException("Deserializing IMessageBase is not supported. This converter is only used for serializing request messages.");
    }

    public override void Write(Utf8JsonWriter writer, IMessageBase value, JsonSerializerOptions options)
    {
        // Serialize the actual concrete type (e.g., TextMessage) instead of just the IMessageBase interface.
        // This ensures all properties of the concrete type are included in the JSON output.
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
