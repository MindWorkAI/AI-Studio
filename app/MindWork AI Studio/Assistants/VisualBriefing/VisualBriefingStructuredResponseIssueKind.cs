using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies a content-free reason why a structured model response could not be accepted.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingStructuredResponseIssueKind>))]
public enum VisualBriefingStructuredResponseIssueKind
{
    /// <summary>No structured-response issue occurred.</summary>
    NONE,
    
    /// <summary>The provider response was empty.</summary>
    EMPTY_RESPONSE,
    
    /// <summary>The JSON root was not an object.</summary>
    ROOT_NOT_OBJECT,
    
    /// <summary>The JSON response ended before the document was complete.</summary>
    UNEXPECTED_END,
    
    /// <summary>Non-whitespace content followed the JSON object.</summary>
    TRAILING_CONTENT,
    
    /// <summary>The candidate contained invalid JSON syntax.</summary>
    INVALID_SYNTAX,
    
    /// <summary>The response contained a field outside the strict contract.</summary>
    UNKNOWN_FIELD,
    
    /// <summary>The response omitted a required field.</summary>
    REQUIRED_FIELD_MISSING,
    
    /// <summary>A field value had the wrong JSON type.</summary>
    TYPE_MISMATCH,
    
    /// <summary>A string did not identify a supported enum value.</summary>
    ENUM_VALUE_INVALID,
    
    /// <summary>The parsed response violated a semantic stage contract.</summary>
    SEMANTIC_CONTRACT_INVALID,
}