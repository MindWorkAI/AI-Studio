using System.Text.Json.Serialization;

namespace AIStudio.Tools;

[JsonConverter(typeof(ContentStreamMetadataJsonConverter))]
public abstract class ContentStreamSseMetadata;