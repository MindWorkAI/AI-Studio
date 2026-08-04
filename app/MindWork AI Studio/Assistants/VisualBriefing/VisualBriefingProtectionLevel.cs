using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingProtectionLevel</c> for the visual briefing feature.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingProtectionLevel>))]
public enum VisualBriefingProtectionLevel
{
    /// <summary>
    /// Defines <c>PUBLIC</c> for the visual briefing feature.
    /// </summary>
    PUBLIC,
    
    /// <summary>
    /// Defines <c>INTERNAL</c> for the visual briefing feature.
    /// </summary>
    INTERNAL,
    
    /// <summary>
    /// Defines <c>PRIVATE</c> for the visual briefing feature.
    /// </summary>
    PRIVATE,
    
    /// <summary>
    /// Defines <c>CONFIDENTIAL</c> for the visual briefing feature.
    /// </summary>
    CONFIDENTIAL,
    
    /// <summary>
    /// Defines <c>OTHER</c> for the visual briefing feature.
    /// </summary>
    OTHER,
}