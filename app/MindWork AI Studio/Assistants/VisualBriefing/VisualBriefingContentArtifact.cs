using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stores an immutable validated content-stage artifact.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingContentArtifact
{
    /// <summary>
    /// Gets or sets the intermediate artifact schema version.
    /// </summary>
    public int ArtifactVersion { get; set; } = VisualBriefingVersions.INTERMEDIATE_ARTIFACT;

    /// <summary>
    /// Gets or sets the content prompt contract version.
    /// </summary>
    public int ContractVersion { get; set; } = VisualBriefingVersions.CONTENT_CONTRACT;

    /// <summary>
    /// Gets or sets the immutable artifact identifier.
    /// </summary>
    public Guid ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the artifact creation time.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the hash of the artifact payload.
    /// </summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the canonical business data.
    /// </summary>
    public JsonElement Data { get; set; }

    /// <summary>
    /// Gets or sets the exactly-once planned slot values.
    /// </summary>
    public List<VisualBriefingSlotValue> Slots { get; set; } = [];

    /// <summary>
    /// Gets or sets typed chart specifications.
    /// </summary>
    public List<VisualBriefingChartSpec> Charts { get; set; } = [];

    /// <summary>
    /// Gets or sets typed interaction controls.
    /// </summary>
    public List<VisualBriefingControlSpec> Controls { get; set; } = [];

    /// <summary>
    /// Gets or sets versioned simulation formulas.
    /// </summary>
    public List<VisualBriefingFormulaSpec> Formulas { get; set; } = [];

    /// <summary>
    /// Gets or sets assistive component descriptions that never become visible.
    /// </summary>
    public Dictionary<string, string> AccessibilityTexts { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets visible source references keyed by component ID.
    /// </summary>
    public Dictionary<string, List<string>> SourceReferences { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the localized label for deterministic simulation reset actions.
    /// </summary>
    public string ResetLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets source coverage.
    /// </summary>
    public List<VisualBriefingSourceCoverage> SourceCoverage { get; set; } = [];

    /// <summary>
    /// Gets or sets the asset plan without embedded bytes.
    /// </summary>
    public List<VisualBriefingAssetPlanItem> AssetPlan { get; set; } = [];

    /// <summary>
    /// Gets or sets the canonical structural signature.
    /// </summary>
    public string StructuralSignature { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the contributing model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;
}