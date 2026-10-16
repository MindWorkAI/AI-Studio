using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingStore
{
    /// <summary>
    /// Starts a new build or resumes the matching persisted build while superseding stale active builds.
    /// </summary>
    /// <param name="candidate">The proposed build identity and fingerprints.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The durable build record and whether it was resumed.</returns>
    public async Task<(VisualBriefingBuildRecord Build, bool Resumed)> StartOrResumeBuildAsync(
        VisualBriefingBuildRecord candidate,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(candidate.BriefingId);
        await gate.WaitAsync(token);
        try
        {
            _ = await this.LoadRequiredWithoutInitializeAsync(candidate.BriefingId, token);
            var builds = await this.LoadBuildsWithoutLockAsync(candidate.BriefingId, token);
            var matching = builds
                .Where(build => build.Status is VisualBriefingBuildStatus.ACTIVE or
                    VisualBriefingBuildStatus.FAILED or
                    VisualBriefingBuildStatus.CANCELED or
                    VisualBriefingBuildStatus.AWAITING_REBUILD)
                .OrderByDescending(build => build.UpdatedAtUtc)
                .FirstOrDefault(build =>
                    build.Mode == candidate.Mode &&
                    build.ParentRevisionId == candidate.ParentRevisionId &&
                    string.Equals(build.InputFingerprint, candidate.InputFingerprint, StringComparison.Ordinal) &&
                    build.ContentContractVersion == candidate.ContentContractVersion &&
                    build.EvidenceContractVersion == candidate.EvidenceContractVersion &&
                    build.PlanContractVersion == candidate.PlanContractVersion &&
                    build.DesignContractVersion == candidate.DesignContractVersion);
            
            if (matching is not null)
            {
                matching.OperationId = candidate.OperationId;
                matching.Status = matching.Status is VisualBriefingBuildStatus.AWAITING_REBUILD
                    ? matching.Status
                    : VisualBriefingBuildStatus.ACTIVE;
                matching.Failure = null;
                matching.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await this.StoreBuildAtomicAsync(matching, overwrite: true, token);
                return (matching, true);
            }

            foreach (var stale in builds.Where(build =>
                         build.Status is VisualBriefingBuildStatus.ACTIVE or
                             VisualBriefingBuildStatus.FAILED or
                             VisualBriefingBuildStatus.CANCELED or
                             VisualBriefingBuildStatus.AWAITING_REBUILD))
            {
                stale.Status = VisualBriefingBuildStatus.SUPERSEDED;
                stale.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await this.StoreBuildAtomicAsync(stale, overwrite: true, token);
            }

            await this.StoreBuildAtomicAsync(candidate, overwrite: false, token);
            return (candidate, false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Persists a build-record update atomically.
    /// </summary>
    /// <param name="build">The build record.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task SaveBuildAsync(VisualBriefingBuildRecord build, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(build.BriefingId);
        await gate.WaitAsync(token);
        
        try
        {
            await this.StoreBuildAtomicAsync(build, overwrite: true, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Loads a persisted build record.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="buildId">The build identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The valid build record, or <see langword="null"/>.</returns>
    public async Task<VisualBriefingBuildRecord?> LoadBuildAsync(
        Guid briefingId,
        Guid buildId,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        return await LoadBuildWithoutLockAsync(this.BuildPath(briefingId, buildId), briefingId, token);
    }

    /// <summary>
    /// Lists build history in reverse update order.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The valid build records.</returns>
    public async Task<IReadOnlyList<VisualBriefingBuildRecord>> ListBuildsAsync(
        Guid briefingId,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var builds = await this.LoadBuildsWithoutLockAsync(briefingId, token);
        return [.. builds.OrderByDescending(build => build.UpdatedAtUtc)];
    }

    /// <summary>
    /// Writes an immutable validated evidence artifact.
    /// </summary>
    public async Task WriteEvidenceArtifactAsync(
        Guid briefingId,
        VisualBriefingEvidenceArtifact artifact,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(briefingId);
        await gate.WaitAsync(token);
        try
        {
            await WriteImmutableArtifactAsync(
                this.EvidenceArtifactPath(briefingId, artifact.ArtifactId),
                JsonSerializer.Serialize(artifact, JSON_OPTIONS),
                token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads and hash-verifies an immutable evidence artifact.
    /// </summary>
    public async Task<VisualBriefingEvidenceArtifact?> ReadEvidenceArtifactAsync(
        Guid briefingId,
        Guid artifactId,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var artifact = await ReadJsonAsync<VisualBriefingEvidenceArtifact>(
            this.EvidenceArtifactPath(briefingId, artifactId),
            token);
        
        if (artifact is null ||
            artifact.ArtifactVersion != VisualBriefingVersions.INTERMEDIATE_ARTIFACT ||
            artifact.ContractVersion != VisualBriefingVersions.EVIDENCE_CONTRACT ||
            artifact.ArtifactId != artifactId)
            return null;
        
        var hash = VisualBriefingPayloadHash.ForEvidence(artifact.Facts, artifact.Metrics, artifact.Tables, artifact.SourceCoverage, artifact.AssetPlan);
        return string.Equals(hash, artifact.PayloadHash, StringComparison.Ordinal) ? artifact : null;
    }

    /// <summary>
    /// Writes an immutable validated plan artifact.
    /// </summary>
    public async Task WritePlanArtifactAsync(
        Guid briefingId,
        VisualBriefingPlanArtifact artifact,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(briefingId);
        await gate.WaitAsync(token);
        try
        {
            await WriteImmutableArtifactAsync(
                this.PlanArtifactPath(briefingId, artifact.ArtifactId),
                JsonSerializer.Serialize(artifact, JSON_OPTIONS),
                token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads and hash-verifies an immutable plan artifact.
    /// </summary>
    public async Task<VisualBriefingPlanArtifact?> ReadPlanArtifactAsync(
        Guid briefingId,
        Guid artifactId,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var artifact = await ReadJsonAsync<VisualBriefingPlanArtifact>(
            this.PlanArtifactPath(briefingId, artifactId),
            token);
        
        if (artifact is null ||
            artifact.ArtifactVersion != VisualBriefingVersions.INTERMEDIATE_ARTIFACT ||
            artifact.ContractVersion != VisualBriefingVersions.PLAN_CONTRACT ||
            artifact.ArtifactId != artifactId)
            return null;
        
        var hash = VisualBriefingPayloadHash.ForPlan(artifact.Sections, artifact.StructuralSignature);

        return string.Equals(hash, artifact.PayloadHash, StringComparison.Ordinal) ? artifact : null;
    }

    /// <summary>
    /// Writes an immutable validated content artifact.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="artifact">The content artifact.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task WriteContentArtifactAsync(
        Guid briefingId,
        VisualBriefingContentArtifact artifact,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(briefingId);
        await gate.WaitAsync(token);
        
        try
        {
            await this.WriteContentArtifactWithoutLockAsync(briefingId, artifact, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads and verifies an immutable content artifact.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The verified artifact, or <see langword="null"/>.</returns>
    public async Task<VisualBriefingContentArtifact?> ReadContentArtifactAsync(
        Guid briefingId,
        Guid artifactId,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var artifact = await ReadJsonAsync<VisualBriefingContentArtifact>(
            this.ContentArtifactPath(briefingId, artifactId),
            token);
        
        if (artifact is null ||
            artifact.ArtifactVersion != VisualBriefingVersions.INTERMEDIATE_ARTIFACT ||
            artifact.ContractVersion != VisualBriefingVersions.CONTENT_CONTRACT ||
            artifact.ArtifactId != artifactId ||
            string.IsNullOrWhiteSpace(artifact.ResetLabel))
            return null;

        var payloadHash = VisualBriefingPayloadHash.ForContent(artifact.Slots, artifact.Charts, artifact.Controls, artifact.Formulas, artifact.AccessibilityTexts,
            artifact.SourceReferences, artifact.ResetLabel, artifact.SourceCoverage, artifact.AssetPlan, artifact.StructuralSignature);

        return string.Equals(payloadHash, artifact.PayloadHash, StringComparison.Ordinal) ? artifact : null;
    }

    /// <summary>
    /// Writes an immutable validated presentation artifact.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="artifact">The presentation artifact.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task WritePresentationArtifactAsync(
        Guid briefingId,
        VisualBriefingPresentationArtifact artifact,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(briefingId);
        await gate.WaitAsync(token);
        
        try
        {
            await this.WritePresentationArtifactWithoutLockAsync(briefingId, artifact, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads and verifies an immutable presentation artifact.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The verified artifact, or <see langword="null"/>.</returns>
    public async Task<VisualBriefingPresentationArtifact?> ReadPresentationArtifactAsync(
        Guid briefingId,
        Guid artifactId,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var artifact = await ReadJsonAsync<VisualBriefingPresentationArtifact>(
            this.PresentationArtifactPath(briefingId, artifactId),
            token);
        
        if (artifact is null ||
            artifact.ArtifactVersion != VisualBriefingVersions.INTERMEDIATE_ARTIFACT ||
            artifact.ContractVersion != VisualBriefingVersions.DESIGN_CONTRACT ||
            artifact.ArtifactId != artifactId)
            return null;

        var payloadHash = VisualBriefingPayloadHash.ForPresentation(artifact.Layout, artifact.Profile, artifact.TemplateHash, artifact.CssHash);
        return string.Equals(payloadHash, artifact.PayloadHash, StringComparison.Ordinal) &&
               string.Equals(
                   VisualBriefingHashing.Compute(artifact.TemplateHtml),
                   artifact.TemplateHash,
                   StringComparison.Ordinal) &&
               string.Equals(
                   VisualBriefingHashing.Compute(artifact.Css),
                   artifact.CssHash,
                   StringComparison.Ordinal)
            ? artifact
            : null;
    }

    /// <summary>
    /// Writes an immutable content artifact while the caller owns the project lock.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="artifact">The content artifact.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task WriteContentArtifactWithoutLockAsync(
        Guid briefingId,
        VisualBriefingContentArtifact artifact,
        CancellationToken token)
    {
        var json = JsonSerializer.Serialize(artifact, JSON_OPTIONS);
        await WriteImmutableArtifactAsync(
            this.ContentArtifactPath(briefingId, artifact.ArtifactId),
            json,
            token);
    }

    /// <summary>
    /// Writes an immutable presentation artifact while the caller owns the project lock.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="artifact">The presentation artifact.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task WritePresentationArtifactWithoutLockAsync(
        Guid briefingId,
        VisualBriefingPresentationArtifact artifact,
        CancellationToken token)
    {
        var json = JsonSerializer.Serialize(artifact, JSON_OPTIONS);
        await WriteImmutableArtifactAsync(
            this.PresentationArtifactPath(briefingId, artifact.ArtifactId),
            json,
            token);
    }

    /// <summary>
    /// Writes one build record atomically.
    /// </summary>
    /// <param name="build">The build record.</param>
    /// <param name="overwrite">Whether an existing record may be replaced.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task StoreBuildAtomicAsync(VisualBriefingBuildRecord build,
        bool overwrite,
        CancellationToken token)
    {
        if (build.BuildVersion != VisualBriefingVersions.BUILD ||
            build.BuildId == Guid.Empty ||
            build.OperationId == Guid.Empty ||
            build.BriefingId == Guid.Empty)
            throw new InvalidDataException("The visual briefing build record is invalid.");

        var json = JsonSerializer.Serialize(build, JSON_OPTIONS);
        await WriteTextAtomicAsync(this.BuildPath(build.BriefingId, build.BuildId), json, overwrite, token);
    }

    /// <summary>
    /// Loads all valid build records without acquiring the project lock.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The valid build records.</returns>
    private async Task<List<VisualBriefingBuildRecord>> LoadBuildsWithoutLockAsync(
        Guid briefingId,
        CancellationToken token)
    {
        List<VisualBriefingBuildRecord> builds = [];
        var directory = this.BuildsDirectory(briefingId);
        if (!Directory.Exists(directory))
            return builds;

        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            token.ThrowIfCancellationRequested();
            var build = await LoadBuildWithoutLockAsync(path, briefingId, token);
            if (build is not null)
                builds.Add(build);
        }
        
        return builds;
    }

    /// <summary>
    /// Loads one valid build record without acquiring the project lock.
    /// </summary>
    /// <param name="path">The build-record path.</param>
    /// <param name="briefingId">The expected briefing identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The build record, or <see langword="null"/>.</returns>
    private static async Task<VisualBriefingBuildRecord?> LoadBuildWithoutLockAsync(
        string path,
        Guid briefingId,
        CancellationToken token)
    {
        var build = await ReadJsonAsync<VisualBriefingBuildRecord>(path, token);
        
        return build is not null &&
               build.BuildVersion == VisualBriefingVersions.BUILD &&
               build.BriefingId == briefingId &&
               build.BuildId != Guid.Empty &&
               build.OperationId != Guid.Empty
            ? build
            : null;
    }
}