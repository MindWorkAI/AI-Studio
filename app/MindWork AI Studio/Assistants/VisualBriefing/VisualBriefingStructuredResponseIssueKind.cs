using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies a content-free reason why a structured model response could not be accepted.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingStructuredResponseIssueKind>))]
public enum VisualBriefingStructuredResponseIssueKind
{
    NONE,
    EMPTY_RESPONSE,
    ROOT_NOT_OBJECT,
    UNEXPECTED_END,
    TRAILING_CONTENT,
    INVALID_SYNTAX,
    UNKNOWN_FIELD,
    REQUIRED_FIELD_MISSING,
    TYPE_MISMATCH,
    ENUM_VALUE_INVALID,
    SEMANTIC_CONTRACT_INVALID,
}