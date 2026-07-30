using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies a bounded chart presentation supported by the chart compiler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingChartKind>))]
public enum VisualBriefingChartKind
{
    /// <summary>Displays values as a line.</summary>
    LINE,
    
    /// <summary>Displays values as a filled area.</summary>
    AREA,
    
    /// <summary>Displays values as vertical bars.</summary>
    BAR,
    
    /// <summary>Displays multiple series as stacked bars.</summary>
    STACKED_BAR,
    
    /// <summary>Displays values as individual points.</summary>
    SCATTER,
    
    /// <summary>Displays proportions as a pie.</summary>
    PIE,
    
    /// <summary>Displays proportions as a ring.</summary>
    DONUT,
    
    /// <summary>Displays multivariate values on radial axes.</summary>
    RADAR,
}