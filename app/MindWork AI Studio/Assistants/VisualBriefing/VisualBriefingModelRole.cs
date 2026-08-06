using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies the role in which a model contributed to a revision.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingModelRole>))]
public enum VisualBriefingModelRole
{
    /// <summary>
    /// The model produced canonical content.
    /// </summary>
    EVIDENCE,

    /// <summary>
    /// The model planned the briefing.
    /// </summary>
    PLAN,

    /// <summary>
    /// The model curated content.
    /// </summary>
    CONTENT,

    /// <summary>
    /// The model designed the layout and visual tokens.
    /// </summary>
    DESIGN,
}