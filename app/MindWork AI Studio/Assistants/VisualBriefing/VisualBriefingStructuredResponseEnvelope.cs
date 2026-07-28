using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies the provider-neutral envelope from which a JSON candidate was obtained.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingStructuredResponseEnvelope>))]
public enum VisualBriefingStructuredResponseEnvelope
{
    RAW_RESPONSE,
    MARKDOWN_JSON_BLOCK,
}