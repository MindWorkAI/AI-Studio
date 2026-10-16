using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Selects one bounded variant of the MindWork visual briefing design system.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingDesignProfile>))]
public enum VisualBriefingDesignProfile
{
    /// <summary>Uses an editorial rhythm suited to narrative storytelling.</summary>
    EDITORIAL,
    
    /// <summary>Uses concise hierarchy suited to decision briefings.</summary>
    EXECUTIVE,
    
    /// <summary>Uses denser presentation suited to evidence-heavy analysis.</summary>
    ANALYTICAL,
}