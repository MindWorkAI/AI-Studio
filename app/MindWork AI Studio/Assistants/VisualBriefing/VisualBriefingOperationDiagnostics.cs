using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Contains user-safe technical details for the most recent operation.
/// </summary>
public sealed class VisualBriefingOperationDiagnostics
{
    /// <summary>
    /// Gets or sets the operation identifier.
    /// </summary>
    public Guid OperationId { get; set; }

    /// <summary>
    /// Gets or sets the build identifier.
    /// </summary>
    public Guid BuildId { get; set; }

    /// <summary>
    /// Gets or sets the current or failed stage.
    /// </summary>
    public VisualBriefingBuildStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the failure code.
    /// </summary>
    public VisualBriefingFailureCode FailureCode { get; set; }

    /// <summary>
    /// Gets or sets the stable validation rule.
    /// </summary>
    public VisualBriefingValidationRule ValidationRule { get; set; }

    /// <summary>
    /// Gets or sets the AI Studio artifact version.
    /// </summary>
    public int ArtifactVersion { get; set; } = VisualBriefingVersions.ARTIFACT;

    /// <summary>
    /// Gets or sets the data schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = VisualBriefingVersions.SCHEMA;

    /// <summary>
    /// Gets or sets the runtime version.
    /// </summary>
    public int RuntimeVersion { get; set; } = VisualBriefingVersions.RUNTIME;

    /// <summary>
    /// Gets or sets the provider family.
    /// </summary>
    public string ProviderFamily { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected model.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the safe structured-response diagnostic.
    /// </summary>
    public VisualBriefingStructuredResponseDiagnostic? StructuredResponse { get; set; }

    /// <summary>
    /// Gets or sets the operation start time.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the operation finish time.
    /// </summary>
    public DateTimeOffset? FinishedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets safe content hashes used for support diagnostics.
    /// </summary>
    public Dictionary<string, string> ContentHashes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets safe intermediate artifact identifiers for support diagnostics.
    /// </summary>
    public Dictionary<string, Guid> ArtifactIds { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Reconstructs clipboard-safe diagnostics from a persistent build record.
    /// </summary>
    /// <param name="build">The persistent build record.</param>
    /// <returns>The reconstructed diagnostics.</returns>
    public static VisualBriefingOperationDiagnostics FromBuildRecord(VisualBriefingBuildRecord build)
    {
        var latestStage = build.Failure?.Stage ??
                          build.Stages
                              .Where(stage => stage.Status is not VisualBriefingBuildStageStatus.NOT_STARTED)
                              .OrderByDescending(stage => stage.Stage)
                              .FirstOrDefault()?.Stage ??
                          VisualBriefingBuildStage.SOURCE_PREPARATION;
        return new()
        {
            OperationId = build.OperationId,
            BuildId = build.BuildId,
            Stage = latestStage,
            FailureCode = build.Failure?.Code ?? VisualBriefingFailureCode.NONE,
            ValidationRule = build.Failure?.ValidationRule ?? VisualBriefingValidationRule.NONE,
            StructuredResponse = build.Failure?.StructuredResponse,
            ProviderFamily = build.ProviderFamily,
            Model = build.Model,
            StartedAtUtc = build.CreatedAtUtc,
            FinishedAtUtc = build.Status is VisualBriefingBuildStatus.ACTIVE
                ? null
                : build.UpdatedAtUtc,
            ContentHashes = build.Stages
                .Where(stage => !string.IsNullOrWhiteSpace(stage.OutputHash))
                .GroupBy(stage => stage.Stage)
                .ToDictionary(
                    group => group.Key.ToString(),
                    group => group.Last().OutputHash,
                    StringComparer.Ordinal),
            ArtifactIds = new Dictionary<string, Guid>(StringComparer.Ordinal)
            {
                ["evidence"] = build.EvidenceArtifactId ?? Guid.Empty,
                ["plan"] = build.PlanArtifactId ?? Guid.Empty,
                ["content"] = build.ContentArtifactId ?? Guid.Empty,
                ["design"] = build.PresentationArtifactId ?? Guid.Empty,
            }
            .Where(item => item.Value != Guid.Empty)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// Serializes the diagnostics without user content.
    /// </summary>
    /// <returns>A compact JSON document suitable for the clipboard.</returns>
    public string ToClipboardText() => JsonSerializer.Serialize(this, VisualBriefingJson.Persistence);
}