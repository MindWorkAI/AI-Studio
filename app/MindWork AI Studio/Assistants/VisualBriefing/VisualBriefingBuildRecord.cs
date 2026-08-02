namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stores durable, resumable build provenance for one briefing operation.
/// </summary>
public sealed class VisualBriefingBuildRecord
{
    /// <summary>
    /// Gets or sets the build-record schema version.
    /// </summary>
    public int BuildVersion { get; init; } = VisualBriefingVersions.BUILD;

    /// <summary>
    /// Gets or sets the build identifier.
    /// </summary>
    public Guid BuildId { get; init; }

    /// <summary>
    /// Gets or sets the operation identifier shown in diagnostics and logs.
    /// </summary>
    public Guid OperationId { get; set; }

    /// <summary>
    /// Gets or sets the owning briefing identifier.
    /// </summary>
    public Guid BriefingId { get; init; }

    /// <summary>
    /// Gets or sets the requested edit mode.
    /// </summary>
    public VisualBriefingEditMode Mode { get; init; }

    /// <summary>
    /// Gets or sets the parent revision identifier.
    /// </summary>
    public Guid? ParentRevisionId { get; init; }

    /// <summary>
    /// Gets or sets the local revision instruction used for recovery.
    /// </summary>
    public string Instruction { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the build lifecycle state.
    /// </summary>
    public VisualBriefingBuildStatus Status { get; set; } = VisualBriefingBuildStatus.ACTIVE;

    /// <summary>
    /// Gets or sets durable stage progress.
    /// </summary>
    public List<VisualBriefingBuildStageRecord> Stages { get; init; } = [];

    /// <summary>
    /// Gets or sets the content artifact identifier.
    /// </summary>
    public Guid? ContentArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the evidence artifact identifier.
    /// </summary>
    public Guid? EvidenceArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the plan artifact identifier.
    /// </summary>
    public Guid? PlanArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the presentation artifact identifier.
    /// </summary>
    public Guid? PresentationArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the revision reserved before assembly.
    /// </summary>
    public Guid? RevisionId { get; set; }

    /// <summary>
    /// Gets or sets the committed revision identifier.
    /// </summary>
    public Guid? CommittedRevisionId { get; set; }

    /// <summary>
    /// Gets or sets the complete safe input fingerprint.
    /// </summary>
    public string InputFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the source and transcript fingerprint.
    /// </summary>
    public string SourceFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the content prompt contract version.
    /// </summary>
    public int ContentContractVersion { get; init; } = VisualBriefingVersions.CONTENT_CONTRACT;

    /// <summary>
    /// Gets or sets the evidence prompt contract version.
    /// </summary>
    public int EvidenceContractVersion { get; init; } = VisualBriefingVersions.EVIDENCE_CONTRACT;

    /// <summary>
    /// Gets or sets the plan prompt contract version.
    /// </summary>
    public int PlanContractVersion { get; init; } = VisualBriefingVersions.PLAN_CONTRACT;

    /// <summary>
    /// Gets or sets the design prompt contract version.
    /// </summary>
    public int DesignContractVersion { get; init; } = VisualBriefingVersions.DESIGN_CONTRACT;

    /// <summary>
    /// Gets or sets the selected provider family.
    /// </summary>
    public string ProviderFamily { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected model name.
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the build creation time.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the most recent build update time.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the terminal or currently recoverable failure.
    /// </summary>
    public VisualBriefingFailure? Failure { get; set; }
}