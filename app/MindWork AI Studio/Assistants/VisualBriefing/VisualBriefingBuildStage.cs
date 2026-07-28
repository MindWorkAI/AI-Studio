using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies a durable stage in the visual briefing build pipeline.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingBuildStage>))]
public enum VisualBriefingBuildStage
{
    /// <summary>
    /// Validates and fingerprints sources and prepares model attachments and visual assets.
    /// </summary>
    SOURCE_PREPARATION,

    /// <summary>
    /// Extracts sourced facts, metrics, tables, coverage, and the asset plan.
    /// </summary>
    EVIDENCE,

    /// <summary>
    /// Plans the storyboard, components, evidence references, and content slots.
    /// </summary>
    PLAN,

    /// <summary>
    /// Fills planned slots, charts, controls, formulas, and accessibility content.
    /// </summary>
    CONTENT,

    /// <summary>
    /// Produces or changes the validated layout DSL and design tokens.
    /// </summary>
    DESIGN,

    /// <summary>
    /// Deterministically compiles layout, components, interactions, charts, CSS, and HTML.
    /// </summary>
    COMPILATION,

    /// <summary>
    /// Deterministically assembles the standalone HTML artifact.
    /// </summary>
    ASSEMBLY,

    /// <summary>
    /// Atomically commits the immutable revision and updates the project manifest.
    /// </summary>
    COMMIT,
}