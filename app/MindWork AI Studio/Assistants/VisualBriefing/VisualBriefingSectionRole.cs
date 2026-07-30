using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies the narrative purpose of a planned briefing section.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSectionRole>))]
public enum VisualBriefingSectionRole
{
    /// <summary>Introduces the briefing and its primary message.</summary>
    HERO,
    
    /// <summary>Summarizes the most important conclusions.</summary>
    EXECUTIVE_SUMMARY,
    
    /// <summary>Develops the briefing's explanatory narrative.</summary>
    NARRATIVE,
    
    /// <summary>Presents supporting facts, metrics, or tables.</summary>
    EVIDENCE,
    
    /// <summary>Provides interactive exploration of the evidence.</summary>
    EXPLORATION,
    
    /// <summary>Closes the briefing with conclusions or next steps.</summary>
    CONCLUSION,
}