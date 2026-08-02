using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Contains deterministic inputs for committing one immutable briefing revision.
/// </summary>
/// <param name="BriefingId">The owning briefing identifier.</param>
/// <param name="ParentRevisionId">The optional parent revision.</param>
/// <param name="EditMode">The revision mode.</param>
/// <param name="Instruction">The local revision instruction.</param>
/// <param name="Data">Canonical business data.</param>
/// <param name="TemplateHtml">The validated declarative template.</param>
/// <param name="Css">The validated presentation stylesheet.</param>
/// <param name="ModelDisplayName">The export-safe fallback model label.</param>
/// <param name="Origin">The local revision origin.</param>
/// <param name="ContentArtifactId">The immutable content artifact identifier.</param>
/// <param name="PresentationArtifactId">The immutable presentation artifact identifier.</param>
/// <param name="BuildId">The persistent build identifier.</param>
/// <param name="OperationId">The operation identifier.</param>
/// <param name="ModelContributions">The export-safe model contributions.</param>
/// <param name="RevisionId">The reserved revision identifier.</param>
/// <param name="CreatedAtUtc">The revision creation time.</param>
/// <param name="EmbeddedAssets">The single protected embedded-asset map.</param>
/// <param name="AssetPlan">The validated visual asset descriptions and alternatives.</param>
/// <param name="EvidenceArtifactId">The immutable evidence artifact identifier.</param>
/// <param name="PlanArtifactId">The immutable plan artifact identifier.</param>
/// <param name="ExportMetadataSource">Optional user-facing export metadata copied from a parent revision.</param>
public sealed record VisualBriefingRevisionRequest(
    Guid BriefingId,
    Guid? ParentRevisionId,
    VisualBriefingEditMode EditMode,
    string Instruction,
    JsonElement Data,
    string TemplateHtml,
    string Css,
    string ModelDisplayName,
    string Origin,
    Guid? ContentArtifactId = null,
    Guid? PresentationArtifactId = null,
    Guid? BuildId = null,
    Guid? OperationId = null,
    IReadOnlyList<VisualBriefingModelContribution>? ModelContributions = null,
    Guid? RevisionId = null,
    DateTimeOffset? CreatedAtUtc = null,
    IReadOnlyDictionary<string, string>? EmbeddedAssets = null,
    IReadOnlyList<VisualBriefingAssetPlanItem>? AssetPlan = null,
    Guid? EvidenceArtifactId = null,
    Guid? PlanArtifactId = null,
    VisualBriefingExportManifest? ExportMetadataSource = null);
