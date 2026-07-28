using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Classifies source coverage reported by the content stage.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSourceCoverageKind>))]
public enum VisualBriefingSourceCoverageKind
{
    /// <summary>
    /// The source directly contributed facts to the briefing.
    /// </summary>
    USED,

    /// <summary>
    /// The source supplied context without directly contributing visible facts.
    /// </summary>
    CONTEXTUAL,

    /// <summary>
    /// The source is intentionally outside the scope requested by the user.
    /// </summary>
    OUT_OF_SCOPE,
}