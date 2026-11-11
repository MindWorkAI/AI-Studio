using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.Google;

public sealed record GoogleEmbeddingResponse
{
    [JsonConverter(typeof(GoogleEmbeddingListConverter))]
    public List<GoogleEmbedding>? Embedding { get; set; }

    /// <remarks>
    /// Internal rather than private: the JSON context of the providers has to reach the converter, else it reads
    /// the embeddings without it.
    /// </remarks>
    internal sealed class GoogleEmbeddingListConverter : JsonConverter<List<GoogleEmbedding>>
    {
        public override List<GoogleEmbedding> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var single = JsonSerializer.Deserialize<GoogleEmbedding>(ref reader, options);
                return single is null ? new() : new() { single };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
                return JsonSerializer.Deserialize<List<GoogleEmbedding>>(ref reader, options) ?? new();

            throw new JsonException("Expected object or array for embedding.");
        }

        public override void Write(Utf8JsonWriter writer, List<GoogleEmbedding> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, options);
    }
}