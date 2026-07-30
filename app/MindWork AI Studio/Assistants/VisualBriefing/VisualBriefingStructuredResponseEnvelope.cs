using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies the provider-neutral envelope from which a JSON candidate was obtained.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingStructuredResponseEnvelope>))]
public enum VisualBriefingStructuredResponseEnvelope
{
    /// <summary>The candidate was extracted from the complete provider response.</summary>
    RAW_RESPONSE,
    
    /// <summary>The candidate was extracted from a fenced Markdown JSON block.</summary>
    MARKDOWN_JSON_BLOCK,
}