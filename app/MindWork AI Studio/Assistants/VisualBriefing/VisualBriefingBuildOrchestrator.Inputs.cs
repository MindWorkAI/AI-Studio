using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Rust;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

internal sealed partial class VisualBriefingBuildOrchestrator
{
    /// <summary>
    /// Loads and verifies the selected parent revision and its intermediate artifacts.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="mode">The edit mode.</param>
    /// <param name="parentRevisionId">The parent revision identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The parent context.</returns>
    private async Task<ParentContext> LoadParentContextAsync(
        VisualBriefingManifest manifest,
        VisualBriefingEditMode mode,
        Guid? parentRevisionId,
        CancellationToken token)
    {
        if (mode is VisualBriefingEditMode.INITIAL)
            return new(null, null, null, null, null, null);
        if (parentRevisionId is null)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                mode is VisualBriefingEditMode.RECOMPILE
                    ? "This briefing version cannot be recompiled with the current AI Studio version. Rebuild the briefing instead."
                    : "The selected parent revision could not be loaded.",
                "A non-initial build has no parent revision ID.");

        var version = manifest.Versions.FirstOrDefault(candidate => candidate.RevisionId == parentRevisionId);
        if (mode is VisualBriefingEditMode.REBUILD)
            return version is not null
                ? new(version, null, null, null, null, null)
                : throw new VisualBriefingBuildException(
                    VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                    VisualBriefingBuildStage.SOURCE_PREPARATION,
                    "The selected parent revision could not be loaded.",
                    "The rebuild parent revision does not exist.");
        var parts = mode is VisualBriefingEditMode.RECOMPILE
            ? await this.store.ReadVersionPartsForRecompileAsync(manifest.BriefingId, parentRevisionId.Value, token)
            : await this.store.ReadVersionPartsAsync(manifest.BriefingId, parentRevisionId.Value, token);
        if (version is null || parts is null ||
            version.EvidenceArtifactId is null ||
            version.PlanArtifactId is null ||
            version.ContentArtifactId is null ||
            version.PresentationArtifactId is null)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                mode is VisualBriefingEditMode.RECOMPILE
                    ? "This briefing version cannot be recompiled with the current AI Studio version. Rebuild the briefing instead."
                    : "The selected parent revision is invalid or incomplete.",
                "The parent revision or its intermediate artifact references are unavailable.");

        var evidence = await this.store.ReadEvidenceArtifactAsync(
            manifest.BriefingId,
            version.EvidenceArtifactId.Value,
            token);
        var plan = await this.store.ReadPlanArtifactAsync(
            manifest.BriefingId,
            version.PlanArtifactId.Value,
            token);
        var content = await this.store.ReadContentArtifactAsync(
            manifest.BriefingId,
            version.ContentArtifactId.Value,
            token);
        var presentation = await this.store.ReadPresentationArtifactAsync(
            manifest.BriefingId,
            version.PresentationArtifactId.Value,
            token);
        if (evidence is null || plan is null || content is null || presentation is null)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                mode is VisualBriefingEditMode.RECOMPILE
                    ? "This briefing version cannot be recompiled with the current AI Studio version. Rebuild the briefing instead."
                    : "The selected parent revision has damaged intermediate artifacts.",
                "A referenced evidence, plan, content, or design artifact failed hash validation.");
        return new(version, parts, evidence, plan, content, presentation);
    }

    /// <summary>
    /// Loads validated evidence for the explicit continue-as-rebuild action.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="buildId">The source build identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The reusable evidence artifact.</returns>
    private async Task<(VisualBriefingEvidenceArtifact Evidence, string SourceFingerprint, string InputFingerprint)> LoadReusableEvidenceAsync(
        Guid briefingId,
        Guid buildId,
        CancellationToken token)
    {
        var sourceBuild = await this.store.LoadBuildAsync(briefingId, buildId, token);
        if (sourceBuild is null ||
            sourceBuild.Status is not VisualBriefingBuildStatus.AWAITING_REBUILD ||
            sourceBuild.EvidenceArtifactId is null)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.CONTENT_SIGNATURE_INCOMPATIBLE,
                VisualBriefingBuildStage.EVIDENCE,
                "The validated evidence is no longer available to continue as a rebuild.",
                "The source build is not awaiting rebuild or has no evidence artifact.");
        var evidence = await this.store.ReadEvidenceArtifactAsync(
                   briefingId,
                   sourceBuild.EvidenceArtifactId.Value,
                   token)
               ?? throw new VisualBriefingBuildException(
                   VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                   VisualBriefingBuildStage.EVIDENCE,
                   "The validated evidence artifact is damaged.",
                   "The reusable evidence artifact failed hash validation.");
        var persistedEvidenceStage = sourceBuild.Stages.FirstOrDefault(stage =>
            stage.Stage is VisualBriefingBuildStage.EVIDENCE &&
            stage.Status is VisualBriefingBuildStageStatus.COMPLETED);
        if (persistedEvidenceStage is null || string.IsNullOrWhiteSpace(persistedEvidenceStage.InputFingerprint))
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                VisualBriefingBuildStage.EVIDENCE,
                "The validated evidence dependencies are unavailable.",
                "The reusable evidence stage has no validated input fingerprint.");
        return (evidence, sourceBuild.SourceFingerprint, persistedEvidenceStage.InputFingerprint);
    }

    /// <summary>
    /// Computes a current source fingerprint including persistent transcript hashes.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The current source fingerprint.</returns>
    private async Task<string> ComputeCurrentSourceFingerprintAsync(
        VisualBriefingManifest manifest,
        CancellationToken token)
    {
        List<string> entries = [];
        foreach (var source in manifest.Sources.OrderBy(source => source.SourceId))
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(source.Path))
                throw new VisualBriefingBuildException(
                    VisualBriefingFailureCode.SOURCE_UNREACHABLE,
                    VisualBriefingBuildStage.SOURCE_PREPARATION,
                    "A briefing source is no longer reachable.",
                    $"Source {source.SourceId:D} failed the reachability check.");
            var sourceHash = await VisualBriefingHashing.ComputeFileAsync(source.Path, token);
            var transcriptHash = string.Empty;
            if (source.IsMedia)
            {
                var transcript = await this.store.ReadTranscriptAsync(manifest.BriefingId, source.SourceId, token);
                if (string.IsNullOrWhiteSpace(transcript) ||
                    source.TranscriptStatus is not VisualBriefingTranscriptStatus.CURRENT)
                    throw new VisualBriefingBuildException(
                        VisualBriefingFailureCode.TRANSCRIPT_UNAVAILABLE,
                        VisualBriefingBuildStage.SOURCE_PREPARATION,
                        "A media transcript is missing or outdated.",
                        $"Transcript status for source {source.SourceId:D} is {source.TranscriptStatus}.");
                transcriptHash = VisualBriefingHashing.Compute(transcript);
            }
            entries.Add(string.Join(
                '\u001f',
                source.SourceId,
                source.Kind,
                source.AssetId,
                sourceHash,
                transcriptHash));
        }
        return VisualBriefingHashing.ComputeSections(
            [manifest.Settings.OptimizeImages.ToString(), .. entries]);
    }

    /// <summary>
    /// Computes the full safe build input fingerprint.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="mode">The edit mode.</param>
    /// <param name="parentRevisionId">The parent revision.</param>
    /// <param name="provider">The provider.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="sourceFingerprint">The source fingerprint.</param>
    /// <param name="reusedContentHash">The optional reused content hash.</param>
    /// <returns>The build input fingerprint.</returns>
    private static string ComputeBuildInputFingerprint(
        VisualBriefingManifest manifest,
        VisualBriefingEditMode mode,
        Guid? parentRevisionId,
        ProviderSettings provider,
        Profile profile,
        string sourceFingerprint,
        string? reusedContentHash) =>
        VisualBriefingHashing.ComputeSections(
            mode.ToString(),
            parentRevisionId?.ToString("D"),
            provider.Id,
            provider.Model.Id,
            profile.Id,
            sourceFingerprint,
            VisualBriefingHashing.Compute(manifest.Settings.Instruction),
            manifest.Settings.TargetLanguage.ToString(),
            manifest.Settings.CustomTargetLanguage,
            manifest.Settings.AudienceProfile.ToString(),
            manifest.Settings.AudienceAgeGroup.ToString(),
            manifest.Settings.AudienceOrganizationalLevel.ToString(),
            manifest.Settings.AudienceExpertise.ToString(),
            manifest.Settings.ShowSourceReferences.ToString(),
            manifest.Settings.OptimizeImages.ToString(),
            manifest.Settings.ProtectionLevel.ToString(),
            VisualBriefingHashing.Compute(manifest.Settings.CustomProtectionLevel),
            reusedContentHash,
            VisualBriefingVersions.EVIDENCE_CONTRACT.ToString(),
            VisualBriefingVersions.PLAN_CONTRACT.ToString(),
            VisualBriefingVersions.CONTENT_CONTRACT.ToString(),
            VisualBriefingVersions.DESIGN_CONTRACT.ToString(),
            VisualBriefingVersions.COMPILER.ToString(),
            VisualBriefingVersions.SCHEMA.ToString(),
            VisualBriefingVersions.RUNTIME.ToString());

    /// <summary>
    /// Validates the selected provider.
    /// </summary>
    /// <param name="provider">The provider.</param>
    private static void ValidateProvider(ProviderSettings provider)
    {
        if (provider == ProviderSettings.NONE || provider.UsedLLMProvider is LLMProviders.NONE)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.PROVIDER_NOT_SELECTED,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                "Please select an LLM provider.",
                "No provider is selected.");
    }

    /// <summary>
    /// Ensures content-generating builds have at least one source-material file.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="mode">The requested edit mode.</param>
    private static void ValidateSourceMaterial(VisualBriefingManifest manifest, VisualBriefingEditMode mode)
    {
        if (mode is VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.RECOMPILE ||
            manifest.Sources.Any(source => source.Kind is VisualBriefingSourceKind.SOURCE_MATERIAL))
        {
            return;
        }

        throw new VisualBriefingBuildException(
            VisualBriefingFailureCode.SOURCE_PREPARATION_FAILED,
            VisualBriefingBuildStage.SOURCE_PREPARATION,
            "Please add at least one source material file.",
            "The briefing has no SOURCE_MATERIAL source.");
    }

    /// <summary>
    /// Validates image-input capabilities for content analysis.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="provider">The provider.</param>
    private static void ValidateVisionCapabilities(
        VisualBriefingManifest manifest,
        ProviderSettings provider)
    {
        var imageSources = manifest.Sources.Where(source =>
            source.Kind is VisualBriefingSourceKind.VISUAL_ASSET ||
            FileTypes.IsAllowedPath(source.Path, FileTypes.IMAGE)).ToArray();
        if (imageSources.Length == 0)
            return;
        var capabilities = provider.GetModelCapabilities();
        var acceptsImages = imageSources.Length == 1
            ? capabilities.Contains(Capability.SINGLE_IMAGE_INPUT) ||
              capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT)
            : capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT);
        if (!acceptsImages)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.MODEL_CAPABILITY_MISSING,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                "The selected model cannot process the number of source images and visual assets.",
                $"ImageCount={imageSources.Length}; SingleImage={capabilities.Contains(Capability.SINGLE_IMAGE_INPUT)}; MultipleImages={capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT)}.");
    }

    /// <summary>
    /// Groups validated parent-revision inputs.
    /// </summary>
    /// <param name="ParentVersion">The local version metadata.</param>
    /// <param name="Parts">The parsed standalone artifact.</param>
    /// <param name="Content">The content artifact.</param>
    /// <param name="Presentation">The presentation artifact.</param>
    private sealed record ParentContext(
        VisualBriefingVersion? ParentVersion,
        VisualBriefingArtifactParts? Parts,
        VisualBriefingEvidenceArtifact? Evidence,
        VisualBriefingPlanArtifact? Plan,
        VisualBriefingContentArtifact? Content,
        VisualBriefingPresentationArtifact? Presentation);
}
