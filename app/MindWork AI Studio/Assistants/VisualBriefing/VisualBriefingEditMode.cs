using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingEditMode</c> for the visual briefing feature.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingEditMode>))]
public enum VisualBriefingEditMode
{
    /// <summary>
    /// Defines <c>INITIAL</c> for the visual briefing feature.
    /// </summary>
    INITIAL,
    /// <summary>
    /// Defines <c>CHANGE_DESIGN</c> for the visual briefing feature.
    /// </summary>
    CHANGE_DESIGN,
    /// <summary>
    /// Defines <c>UPDATE_CONTENT</c> for the visual briefing feature.
    /// </summary>
    UPDATE_CONTENT,
    /// <summary>
    /// Defines <c>REBUILD</c> for the visual briefing feature.
    /// </summary>
    REBUILD,

    /// <summary>
    /// Reuses the selected revision's semantic artifacts and runs only the current compiler,
    /// standalone runtime assembly, and immutable commit stages.
    /// </summary>
    RECOMPILE,

    /// <summary>
    /// Defines <c>IMPORT</c> for the visual briefing feature.
    /// </summary>
    IMPORT,
}