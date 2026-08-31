using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stores an immutable resolved presentation-stage artifact.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingPresentationArtifact
{
    /// <summary>
    /// Gets or sets the intermediate artifact schema version.
    /// </summary>
    public int ArtifactVersion { get; set; } = VisualBriefingVersions.INTERMEDIATE_ARTIFACT;

    /// <summary>
    /// Gets or sets the design prompt contract version.
    /// </summary>
    public int ContractVersion { get; set; } = VisualBriefingVersions.DESIGN_CONTRACT;

    /// <summary>
    /// Gets or sets the immutable artifact identifier.
    /// </summary>
    public Guid ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the artifact creation time.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the hash of the resolved presentation payload.
    /// </summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the validated layout DSL.
    /// </summary>
    public VisualBriefingLayoutNode Layout { get; set; } = new();

    /// <summary>
    /// Gets or sets the bounded MindWork editorial design profile.
    /// </summary>
    public VisualBriefingDesignProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the complete declarative HTML template.
    /// </summary>
    public string TemplateHtml { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the complete safe stylesheet.
    /// </summary>
    public string Css { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deterministic template hash.
    /// </summary>
    public string TemplateHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deterministic CSS hash.
    /// </summary>
    public string CssHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the contributing model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;
}