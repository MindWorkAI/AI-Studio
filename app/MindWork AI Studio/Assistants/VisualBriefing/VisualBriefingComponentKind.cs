using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies a semantic component supported by the deterministic briefing compiler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingComponentKind>))]
public enum VisualBriefingComponentKind
{
    /// <summary>Displays narrative text.</summary>
    TEXT,
    
    /// <summary>Highlights one metric and its context.</summary>
    METRIC,
    
    /// <summary>Displays tabular data.</summary>
    TABLE,
    
    /// <summary>Visualizes numeric series with Apache ECharts.</summary>
    CHART,
    
    /// <summary>Displays one embedded visual asset.</summary>
    ASSET,
    
    /// <summary>Emphasizes a concise insight or warning.</summary>
    CALLOUT,
    
    /// <summary>Organizes panels behind tab controls.</summary>
    TABS,
    
    /// <summary>Organizes panels in expandable sections.</summary>
    ACCORDION,
    
    /// <summary>Displays searchable and sortable tabular data.</summary>
    FILTERABLE_TABLE,
    
    /// <summary>Provides deterministic interactive controls and calculated results.</summary>
    SIMULATION,

    /// <summary>Displays an ordered chronological sequence without a chart runtime.</summary>
    TIMELINE,
}