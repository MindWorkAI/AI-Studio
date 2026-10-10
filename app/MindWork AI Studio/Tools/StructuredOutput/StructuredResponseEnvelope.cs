using System.Text.Json.Serialization;

namespace AIStudio.Tools;

/// <summary>
/// Identifies the provider-neutral envelope from which a JSON candidate was obtained.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StructuredResponseEnvelope>))]
public enum StructuredResponseEnvelope
{
    /// <summary>The candidate was extracted from the complete provider response.</summary>
    RAW_RESPONSE,

    /// <summary>The candidate was extracted from a fenced Markdown JSON block.</summary>
    MARKDOWN_JSON_BLOCK,
}