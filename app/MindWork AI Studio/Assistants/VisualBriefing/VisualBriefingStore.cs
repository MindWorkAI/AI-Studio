using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.Rust;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingStore</c> for the visual briefing feature.
/// </summary>
public sealed class VisualBriefingStore(
    VisualBriefingArtifactService artifactService,
    ILogger<VisualBriefingStore> logger,
    VisualBriefingStorageOptions? storageOptions = null)
{
    /// <summary>Defines the project manifest filename.</summary>
    private const string MANIFEST_FILE_NAME = "manifest.json";
    
    /// <summary>Defines the last-selection filename.</summary>
    private const string SELECTION_FILE_NAME = "selection.json";
    
    /// <summary>Defines the intermediate-artifact directory.</summary>
    private const string ARTIFACTS_DIRECTORY_NAME = "artifacts";
    
    /// <summary>Defines the evidence-artifact directory.</summary>
    private const string EVIDENCE_ARTIFACTS_DIRECTORY_NAME = "evidence";
    
    /// <summary>Defines the plan-artifact directory.</summary>
    private const string PLAN_ARTIFACTS_DIRECTORY_NAME = "plan";
    
    /// <summary>Defines the content-artifact directory.</summary>
    private const string CONTENT_ARTIFACTS_DIRECTORY_NAME = "content";
    
    /// <summary>Defines the presentation-artifact directory.</summary>
    private const string PRESENTATION_ARTIFACTS_DIRECTORY_NAME = "presentation";
    
    /// <summary>Defines the build-history directory.</summary>
    private const string BUILDS_DIRECTORY_NAME = "builds";
    
    /// <summary>Defines the immutable-version directory.</summary>
    private const string VERSIONS_DIRECTORY_NAME = "versions";
    
    /// <summary>Defines the persistent-transcript directory.</summary>
    private const string TRANSCRIPTS_DIRECTORY_NAME = "transcripts";
    
    /// <summary>Gets the shared persistence JSON options.</summary>
    private static readonly JsonSerializerOptions JSON_OPTIONS = VisualBriefingJson.Indented;
    
    /// <summary>Stores per-project process locks.</summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> briefingLocks = [];
    
    /// <summary>
    /// Serializes store initialization.
    /// </summary>
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    
    /// <summary>
    /// Serializes last-selection writes.
    /// </summary>
    private readonly SemaphoreSlim selectionLock = new(1, 1);
    
    /// <summary>Tracks whether initialization and reconciliation completed.</summary>
    private bool initialized;

    /// <summary>
    /// Defines <c>LastSelectedBriefingId</c> for the visual briefing feature.
    /// </summary>
    public Guid? LastSelectedBriefingId { get; private set; }

    /// <summary>
    /// Defines <c>RootDirectory</c> for the visual briefing feature.
    /// </summary>
    public string RootDirectory => Path.Combine(
        storageOptions?.DataDirectory ??
        SettingsManager.DataDirectory ??
        throw new InvalidOperationException("The AI Studio data directory is not initialized."),
        "visualBriefings");

    /// <summary>
    /// Defines <c>RememberSelectionAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task RememberSelectionAsync(Guid briefingId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        if (this.LastSelectedBriefingId == briefingId)
            return;

        await this.selectionLock.WaitAsync(token);
        try
        {
            this.LastSelectedBriefingId = briefingId;
            await WriteTextAtomicAsync(this.SelectionPath(), JsonSerializer.Serialize<Guid?>(briefingId), token);
        }
        finally
        {
            this.selectionLock.Release();
        }
    }

    /// <summary>
    /// Defines <c>ForgetSelectionAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task ForgetSelectionAsync(Guid briefingId, CancellationToken token = default)
    {
        if (this.LastSelectedBriefingId != briefingId)
            return;

        await this.selectionLock.WaitAsync(token);
        try
        {
            if (this.LastSelectedBriefingId != briefingId)
                return;

            this.LastSelectedBriefingId = null;
            await WriteTextAtomicAsync(this.SelectionPath(), JsonSerializer.Serialize<Guid?>(null), token);
        }
        finally
        {
            this.selectionLock.Release();
        }
    }

    /// <summary>
    /// Defines <c>InitializeAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task InitializeAsync(CancellationToken token = default)
    {
        if (this.initialized)
            return;

        await this.initializationLock.WaitAsync(token);
        try
        {
            if (this.initialized)
                return;

            Directory.CreateDirectory(this.RootDirectory);
            foreach (var temporaryPath in Directory.EnumerateFiles(this.RootDirectory, "*.tmp-*", SearchOption.AllDirectories))
                TryDeleteFile(temporaryPath);

            await this.LoadSelectionAsync(token);
            foreach (var directory in Directory.EnumerateDirectories(this.RootDirectory))
            {
                token.ThrowIfCancellationRequested();
                if (!Guid.TryParse(Path.GetFileName(directory), out var briefingId))
                    continue;

                await this.ReconcileAsync(briefingId, token);
            }

            this.initialized = true;
        }
        finally
        {
            this.initializationLock.Release();
        }
    }

    /// <summary>
    /// Defines <c>ListAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<IReadOnlyList<VisualBriefingManifest>> ListAsync(CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        List<VisualBriefingManifest> manifests = [];
        foreach (var directory in Directory.EnumerateDirectories(this.RootDirectory))
        {
            token.ThrowIfCancellationRequested();
            if (!Guid.TryParse(Path.GetFileName(directory), out var briefingId))
                continue;

            var manifest = await this.LoadAsync(briefingId, token);
            if (manifest is not null)
                manifests.Add(manifest);
        }

        return manifests.OrderByDescending(manifest => manifest.ModifiedAtUtc).ToArray();
    }

    /// <summary>
    /// Defines <c>LoadAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<VisualBriefingManifest?> LoadAsync(Guid briefingId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var path = this.ManifestPath(briefingId);
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            var manifest = await JsonSerializer.DeserializeAsync<VisualBriefingManifest>(stream, JSON_OPTIONS, token);
            if (manifest is null || !IsValidManifest(manifest, briefingId))
                return null;

            RefreshSourceStatuses(manifest);
            return manifest;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(
                new EventId((int)VisualBriefingLogEventId.STORE_REJECTED, VisualBriefingLogEventId.STORE_REJECTED.ToString()),
                "Could not load visual briefing manifest. BriefingId={BriefingId} ExceptionType={ExceptionType}",
                briefingId,
                exception.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// Defines <c>CreateAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<VisualBriefingManifest> CreateAsync(
        string name,
        string author,
        VisualBriefingLocalSettings settings,
        Guid? briefingId = null,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A briefing name is required.", nameof(name));

        var id = briefingId ?? Guid.NewGuid();
        var gate = this.GetLock(id);
        await gate.WaitAsync(token);
        try
        {
            var directory = this.BriefingDirectory(id);
            if (Directory.Exists(directory))
                throw new IOException($"A visual briefing with ID '{id}' already exists.");

            Directory.CreateDirectory(this.VersionsDirectory(id));
            Directory.CreateDirectory(this.TranscriptsDirectory(id));
            Directory.CreateDirectory(this.EvidenceArtifactsDirectory(id));
            Directory.CreateDirectory(this.PlanArtifactsDirectory(id));
            Directory.CreateDirectory(this.ContentArtifactsDirectory(id));
            Directory.CreateDirectory(this.PresentationArtifactsDirectory(id));
            Directory.CreateDirectory(this.BuildsDirectory(id));
            var now = DateTimeOffset.UtcNow;
            var manifest = new VisualBriefingManifest
            {
                BriefingId = id,
                Name = name.Trim(),
                Author = author.Trim(),
                CreatedAtUtc = now,
                ModifiedAtUtc = now,
                Settings = settings,
            };
            await this.StoreManifestAtomicAsync(manifest, token);
            return manifest;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Defines <c>RenameAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task RenameAsync(Guid briefingId, string name, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A briefing name is required.", nameof(name));

        await this.MutateManifestAsync(briefingId, manifest =>
        {
            manifest.Name = name.Trim();
            manifest.ModifiedAtUtc = DateTimeOffset.UtcNow;
        }, token);
    }

    /// <summary>
    /// Defines <c>SaveProjectAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task SaveProjectAsync(
        Guid briefingId,
        string name,
        string author,
        VisualBriefingLocalSettings settings,
        IEnumerable<(string Path, VisualBriefingSourceKind Kind)> sources,
        CancellationToken token = default)
    {
        await this.MutateManifestAsync(briefingId, manifest =>
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("A briefing name is required.");

            manifest.Name = name.Trim();
            manifest.Author = author.Trim();
            manifest.Settings = settings;
            var mergedSources = MergeSources(manifest.Sources, sources);
            var retainedSourceIds = mergedSources.Select(source => source.SourceId).ToHashSet();
            
            foreach (var removedSource in manifest.Sources.Where(source => !retainedSourceIds.Contains(source.SourceId)))
                TryDeleteFile(this.TranscriptPath(briefingId, removedSource.SourceId));
            
            manifest.Sources = mergedSources;
            manifest.ModifiedAtUtc = DateTimeOffset.UtcNow;
            RefreshSourceStatuses(manifest);
        }, token);
    }

    /// <summary>
    /// Defines <c>RelinkSourceAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task RelinkSourceAsync(Guid briefingId, Guid sourceId, string newPath, CancellationToken token = default)
    {
        if (!File.Exists(newPath))
            throw new FileNotFoundException("The replacement source is not reachable.", newPath);
        
        if (!IsSupportedSourcePath(newPath))
            throw new InvalidDataException("The replacement file type is not supported as briefing source material.");

        await this.MutateManifestAsync(briefingId, manifest =>
        {
            var source = manifest.Sources.FirstOrDefault(candidate => candidate.SourceId == sourceId)
                ?? throw new InvalidOperationException("The source does not exist in this briefing.");
            
            if (source.Kind is VisualBriefingSourceKind.VISUAL_ASSET &&
                !FileTypes.IsAllowedPath(newPath, FileTypes.VISUAL_BRIEFING_IMAGE))
                throw new InvalidDataException("Visual assets must be PNG, JPEG, or WebP files.");

            var wasMedia = source.IsMedia;
            ApplyFileSnapshot(source, newPath);
            
            if (source.IsMedia)
                source.TranscriptStatus = VisualBriefingTranscriptStatus.OUTDATED;
            else
            {
                source.TranscriptStatus = VisualBriefingTranscriptStatus.NOT_REQUIRED;
                if (wasMedia)
                    TryDeleteFile(this.TranscriptPath(briefingId, source.SourceId));
            }
            
            manifest.ModifiedAtUtc = DateTimeOffset.UtcNow;
        }, token);
    }

    /// <summary>
    /// Defines <c>RemoveSourceAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task RemoveSourceAsync(Guid briefingId, Guid sourceId, CancellationToken token = default)
    {
        await this.MutateManifestAsync(briefingId, manifest =>
        {
            var source = manifest.Sources.FirstOrDefault(candidate => candidate.SourceId == sourceId);
            if (source is null)
                return;

            manifest.Sources.Remove(source);
            TryDeleteFile(this.TranscriptPath(briefingId, source.SourceId));
            manifest.ModifiedAtUtc = DateTimeOffset.UtcNow;
        }, token);
    }

    /// <summary>
    /// Defines <c>SetTranscriptCurrentAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task SetTranscriptCurrentAsync(Guid briefingId, Guid sourceId, string transcript, CancellationToken token = default)
    {
        var gate = this.GetLock(briefingId);
        await gate.WaitAsync(token);
        try
        {
            var manifest = await this.LoadRequiredWithoutInitializeAsync(briefingId, token);
            var source = manifest.Sources.FirstOrDefault(candidate => candidate.SourceId == sourceId)
                ?? throw new InvalidOperationException("The media source does not exist in this briefing.");
            var transcriptPath = this.TranscriptPath(briefingId, source.SourceId);
            await WriteTextAtomicAsync(transcriptPath, transcript, token);
            source.TranscriptStatus = VisualBriefingTranscriptStatus.CURRENT;
            ApplyFileSnapshot(source, source.Path);
            manifest.ModifiedAtUtc = DateTimeOffset.UtcNow;
            await this.StoreManifestAtomicAsync(manifest, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Defines <c>ReadTranscriptAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<string?> ReadTranscriptAsync(Guid briefingId, Guid sourceId, CancellationToken token = default)
    {
        var path = this.TranscriptPath(briefingId, sourceId);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, token) : null;
    }

    /// <summary>
    /// Defines <c>GetTranscriptPath</c> for the visual briefing feature.
    /// </summary>
    public string GetTranscriptPath(Guid briefingId, Guid sourceId) => this.TranscriptPath(briefingId, sourceId);

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
                await this.StoreBuildAtomicAsync(matching, token);
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
                await this.StoreBuildAtomicAsync(stale, token);
            }

            await this.StoreBuildAtomicAsync(candidate, token, overwrite: false);
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
            await this.StoreBuildAtomicAsync(build, token);
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
        return await this.LoadBuildWithoutLockAsync(this.BuildPath(briefingId, buildId), briefingId, token);
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
        return builds.OrderByDescending(build => build.UpdatedAtUtc).ToArray();
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
        var hash = VisualBriefingHashing.ComputeSections(
            JsonSerializer.Serialize(artifact.Facts, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.Metrics, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.Tables, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.SourceCoverage, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.AssetPlan, VisualBriefingJson.Compact));
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
        
        var hash = VisualBriefingHashing.ComputeSections(
            JsonSerializer.Serialize(artifact.Sections, VisualBriefingJson.Compact),
            artifact.StructuralSignature);
        
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

        var payloadHash = VisualBriefingHashing.ComputeSections(
            JsonSerializer.Serialize(artifact.Slots, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.Charts, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.Controls, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.Formulas, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.AccessibilityTexts, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.VisibleLabels, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.SourceReferences, VisualBriefingJson.Compact),
            artifact.ResetLabel,
            JsonSerializer.Serialize(artifact.CustomLanguageLabels, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.SourceCoverage, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.AssetPlan, VisualBriefingJson.Compact),
            artifact.StructuralSignature);
        
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

        var payloadHash = VisualBriefingHashing.ComputeSections(
            JsonSerializer.Serialize(artifact.Layout, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(artifact.Tokens, VisualBriefingJson.Compact),
            artifact.TemplateHash,
            artifact.CssHash);
        
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
    /// Defines <c>FindSourceIdByPathAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<Guid?> FindSourceIdByPathAsync(Guid briefingId, string path, CancellationToken token = default)
    {
        var manifest = await this.LoadAsync(briefingId, token);
        if (manifest is null)
            return null;

        var fullPath = Path.GetFullPath(path);
        return manifest.Sources.FirstOrDefault(source =>
            PathComparer().Equals(Path.GetFullPath(source.Path), fullPath))?.SourceId;
    }

    /// <summary>
    /// Defines <c>AddRevisionAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<VisualBriefingRevisionResult> AddRevisionAsync(
        VisualBriefingRevisionRequest request,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(request.BriefingId);
        await gate.WaitAsync(token);
        
        try
        {
            var manifest = await this.LoadRequiredWithoutInitializeAsync(request.BriefingId, token);
            RefreshSourceStatuses(manifest);
            var blockingSources = manifest.Sources
                .Where(source => source.Status is VisualBriefingSourceStatus.UNREACHABLE or VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED)
                .ToArray();
            
            if (request.EditMode is not VisualBriefingEditMode.CHANGE_DESIGN && blockingSources.Length > 0)
                return VisualBriefingRevisionResult.Failure("One or more sources are missing or have an outdated transcript.");

            var parent = request.ParentRevisionId is null
                ? null
                : manifest.Versions.FirstOrDefault(version => version.RevisionId == request.ParentRevisionId);
            
            if (request.EditMode is not VisualBriefingEditMode.INITIAL && parent is null)
                return VisualBriefingRevisionResult.Failure("The selected parent revision no longer exists.");
            
            VisualBriefingArtifactParts? parentParts = null;
            if (parent is not null)
            {
                parentParts = await this.ReadVersionPartsAsync(manifest.BriefingId, parent.RevisionId, token);
                if (parentParts is null)
                    return VisualBriefingRevisionResult.Failure("The selected parent revision is invalid or damaged.");

                var parentHashes = ComputeSectionHashes(parentParts);
                if (!string.Equals(parent.DataHash, parentHashes.DataHash, StringComparison.Ordinal) ||
                    !string.Equals(parent.AssetHash, parentHashes.AssetHash, StringComparison.Ordinal) ||
                    !string.Equals(parent.TemplateHash, parentHashes.TemplateHash, StringComparison.Ordinal) ||
                    !string.Equals(parent.CssHash, parentHashes.CssHash, StringComparison.Ordinal) ||
                    !string.Equals(parent.RuntimeHash, parentHashes.RuntimeHash, StringComparison.Ordinal))
                    return VisualBriefingRevisionResult.Failure("The selected parent revision does not match its protected section hashes.");
            }

            var preserveRuntime = request.EditMode is VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.UPDATE_CONTENT;
            var html = await artifactService.BuildAsync(
                manifest,
                request,
                preserveRuntime ? parentParts?.RuntimeScript : null,
                preserveRuntime ? parentParts?.EChartsScript : null,
                token);
            
            if (!artifactService.TryParse(html, out var parts, out var parseIssue))
                return VisualBriefingRevisionResult.Failure(parseIssue);

            var hashes = ComputeSectionHashes(parts);
            if (parent is not null)
            {
                if (request.EditMode is VisualBriefingEditMode.CHANGE_DESIGN &&
                    (!string.Equals(parent.DataHash, hashes.DataHash, StringComparison.Ordinal) ||
                     !string.Equals(parent.AssetHash, hashes.AssetHash, StringComparison.Ordinal) ||
                     !string.Equals(parent.RuntimeHash, hashes.RuntimeHash, StringComparison.Ordinal)))
                    return VisualBriefingRevisionResult.Failure("A design change attempted to modify facts, embedded assets, or the runtime.");

                if (request.EditMode is VisualBriefingEditMode.UPDATE_CONTENT &&
                    (!string.Equals(parent.TemplateHash, hashes.TemplateHash, StringComparison.Ordinal) ||
                     !string.Equals(parent.CssHash, hashes.CssHash, StringComparison.Ordinal) ||
                     !string.Equals(parent.RuntimeHash, hashes.RuntimeHash, StringComparison.Ordinal)))
                    return VisualBriefingRevisionResult.Failure("A content update attempted to modify the template, CSS, or runtime.");

                if (string.Equals(parent.DataHash, hashes.DataHash, StringComparison.Ordinal) &&
                    string.Equals(parent.AssetHash, hashes.AssetHash, StringComparison.Ordinal) &&
                    string.Equals(parent.TemplateHash, hashes.TemplateHash, StringComparison.Ordinal) &&
                    string.Equals(parent.CssHash, hashes.CssHash, StringComparison.Ordinal) &&
                    string.Equals(parent.RuntimeHash, hashes.RuntimeHash, StringComparison.Ordinal))
                    return VisualBriefingRevisionResult.Failure("The model response did not change the briefing.");
            }

            var version = new VisualBriefingVersion
            {
                VersionNumber = this.NextVersionNumber(manifest),
                RevisionId = parts.ExportManifest.RevisionId,
                ParentRevisionId = request.ParentRevisionId,
                CreatedAtUtc = parts.ExportManifest.CreatedAtUtc,
                EditMode = request.EditMode,
                Instruction = request.Instruction,
                PayloadHash = parts.PayloadHash,
                Origin = request.Origin,
                DataHash = hashes.DataHash,
                AssetHash = hashes.AssetHash,
                TemplateHash = hashes.TemplateHash,
                CssHash = hashes.CssHash,
                RuntimeHash = hashes.RuntimeHash,
                ContentArtifactId = request.ContentArtifactId,
                PresentationArtifactId = request.PresentationArtifactId,
                EvidenceArtifactId = request.EvidenceArtifactId,
                PlanArtifactId = request.PlanArtifactId,
                BuildId = request.BuildId,
                OperationId = request.OperationId,
                ModelContributions = request.ModelContributions?.ToList() ?? [],
            };
            
            version.FileName = $"{version.VersionNumber:000000}-{version.RevisionId:D}.html";
            await WriteTextAtomicAsync(
                Path.Combine(this.VersionsDirectory(manifest.BriefingId), version.FileName),
                html,
                token,
                overwrite: false);
            
            manifest.Versions.Add(version);
            if (request.EditMode is not VisualBriefingEditMode.CHANGE_DESIGN)
                foreach (var source in manifest.Sources.Where(source => File.Exists(source.Path)))
                    ApplyFileSnapshot(source, source.Path);
            
            manifest.ModifiedAtUtc = version.CreatedAtUtc;
            await this.StoreManifestAtomicAsync(manifest, token);
            return new(true, version, string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            logger.LogWarning(
                new EventId((int)VisualBriefingLogEventId.STORE_REJECTED, VisualBriefingLogEventId.STORE_REJECTED.ToString()),
                "Could not create a visual briefing revision. BriefingId={BriefingId} BuildId={BuildId} OperationId={OperationId} ExceptionType={ExceptionType}",
                request.BriefingId,
                request.BuildId,
                request.OperationId,
                exception.GetType().Name);
            
            var safeIssue = exception is InvalidDataException
                ? exception.Message
                : "The visual briefing version could not be stored.";
            
            return VisualBriefingRevisionResult.Failure(safeIssue);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Defines <c>GetVersionPathAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<string?> GetVersionPathAsync(Guid briefingId, Guid revisionId, CancellationToken token = default)
    {
        var manifest = await this.LoadAsync(briefingId, token);
        var version = manifest?.Versions.FirstOrDefault(candidate => candidate.RevisionId == revisionId);
        if (version is null)
            return null;

        var path = this.VersionPath(briefingId, version);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Defines <c>ReadVersionPartsAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<VisualBriefingArtifactParts?> ReadVersionPartsAsync(Guid briefingId, Guid revisionId, CancellationToken token = default)
    {
        var manifest = await this.LoadAsync(briefingId, token);
        var version = manifest?.Versions.FirstOrDefault(candidate => candidate.RevisionId == revisionId);
        if (version is null)
            return null;

        var path = this.VersionPath(briefingId, version);
        if (!File.Exists(path))
            return null;

        var html = await File.ReadAllTextAsync(path, token);
        if (!artifactService.TryParse(html, out var parts, out _) ||
            parts.ExportManifest.BriefingId != briefingId ||
            parts.ExportManifest.RevisionId != revisionId ||
            !string.Equals(parts.PayloadHash, version.PayloadHash, StringComparison.OrdinalIgnoreCase))
            return null;

        return parts;
    }

    /// <summary>
    /// Opens a validated immutable version for direct streaming.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="revisionId">The revision identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The positioned stream and parsed artifact, or <see langword="null"/>.</returns>
    public async Task<(FileStream Stream, VisualBriefingArtifactParts Parts)?> OpenValidatedVersionAsync(
        Guid briefingId,
        Guid revisionId,
        CancellationToken token = default)
    {
        var manifest = await this.LoadAsync(briefingId, token);
        var version = manifest?.Versions.FirstOrDefault(candidate => candidate.RevisionId == revisionId);
        if (version is null)
            return null;

        var path = this.VersionPath(briefingId, version);
        if (!File.Exists(path))
            return null;

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 65_536, leaveOpen: true);
            var html = await reader.ReadToEndAsync(token);
            if (!artifactService.TryParse(html, out var parts, out _) ||
                parts.ExportManifest.BriefingId != briefingId ||
                parts.ExportManifest.RevisionId != revisionId ||
                !string.Equals(parts.PayloadHash, version.PayloadHash, StringComparison.OrdinalIgnoreCase))
            {
                await stream.DisposeAsync();
                return null;
            }

            stream.Position = 0;
            return (stream, parts);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Defines <c>ImportAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<VisualBriefingImportResult> ImportAsync(string sourcePath, bool importNameConflictAsCopy, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var html = await File.ReadAllTextAsync(sourcePath, token);
        if (!artifactService.TryParse(html, out var parts, out var issue))
            return new(false, Guid.Empty, Guid.Empty, false, false, issue);

        var export = parts.ExportManifest;
        var existing = await this.LoadAsync(export.BriefingId, token);
        if (existing is not null && !NamesEqual(existing.Name, export.Name))
        {
            if (!importNameConflictAsCopy)
                return new(false, existing.BriefingId, export.RevisionId, true, false, "The briefing ID exists locally under a different name.");

            return await this.ImportCopyAsync(parts, token);
        }

        if (existing is null)
        {
            existing = await this.CreateAsync(
                export.Name,
                export.Author,
                SettingsFromExport(export),
                export.BriefingId,
                token);
        }

        var gate = this.GetLock(existing.BriefingId);
        await gate.WaitAsync(token);
        try
        {
            existing = await this.LoadRequiredWithoutInitializeAsync(existing.BriefingId, token);
            var knownRevision = existing.Versions.FirstOrDefault(version => version.RevisionId == export.RevisionId);
            if (knownRevision is not null)
            {
                if (string.Equals(knownRevision.PayloadHash, parts.PayloadHash, StringComparison.OrdinalIgnoreCase))
                {
                    if (await this.ReadVersionPartsAsync(existing.BriefingId, knownRevision.RevisionId, token) is null)
                    {
                        await WriteTextAtomicAsync(this.VersionPath(existing.BriefingId, knownRevision), html, token);
                        var restoredHashes = ComputeSectionHashes(parts);
                        knownRevision.DataHash = restoredHashes.DataHash;
                        knownRevision.AssetHash = restoredHashes.AssetHash;
                        knownRevision.TemplateHash = restoredHashes.TemplateHash;
                        knownRevision.CssHash = restoredHashes.CssHash;
                        knownRevision.RuntimeHash = restoredHashes.RuntimeHash;
                        existing.ModifiedAtUtc = DateTimeOffset.UtcNow;
                        await this.StoreManifestAtomicAsync(existing, token);
                    }

                    return new(true, existing.BriefingId, export.RevisionId, false, true, string.Empty);
                }

                return new(false, existing.BriefingId, export.RevisionId, false, false, "The revision ID exists with a different payload hash.");
            }

            var hashes = ComputeSectionHashes(parts);
            var importedArtifacts = await this.MaterializeImportedArtifactsAsync(
                existing.BriefingId,
                parts,
                projectLockHeld: true,
                token: token);
            
            var version = new VisualBriefingVersion
            {
                VersionNumber = this.NextVersionNumber(existing),
                RevisionId = export.RevisionId,
                ParentRevisionId = export.ParentRevisionId,
                CreatedAtUtc = export.CreatedAtUtc,
                EditMode = VisualBriefingEditMode.IMPORT,
                PayloadHash = parts.PayloadHash,
                Origin = Path.GetFileName(sourcePath),
                DataHash = hashes.DataHash,
                AssetHash = hashes.AssetHash,
                TemplateHash = hashes.TemplateHash,
                CssHash = hashes.CssHash,
                RuntimeHash = hashes.RuntimeHash,
                ContentArtifactId = importedArtifacts.Content.ArtifactId,
                PresentationArtifactId = importedArtifacts.Presentation.ArtifactId,
                ModelContributions =
                [
                    new(VisualBriefingModelRole.CONTENT, importedArtifacts.Content.Model),
                    new(VisualBriefingModelRole.DESIGN, importedArtifacts.Presentation.Model),
                ],
            };
            
            version.FileName = $"{version.VersionNumber:000000}-{version.RevisionId:D}.html";
            await WriteTextAtomicAsync(
                Path.Combine(this.VersionsDirectory(existing.BriefingId), version.FileName),
                html,
                token,
                overwrite: false);
            
            existing.Versions.Add(version);
            existing.ModifiedAtUtc = DateTimeOffset.UtcNow;
            await this.StoreManifestAtomicAsync(existing, token);
            return new(true, existing.BriefingId, version.RevisionId, false, false, string.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Defines <c>DeleteAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task DeleteAsync(Guid briefingId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(briefingId);
        await gate.WaitAsync(token);
        try
        {
            var directory = this.BriefingDirectory(briefingId);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Defines <c>ImportCopyAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task<VisualBriefingImportResult> ImportCopyAsync(VisualBriefingArtifactParts parts, CancellationToken token)
    {
        var copyId = Guid.NewGuid();
        var manifest = await this.CreateAsync(
            parts.ExportManifest.Name,
            parts.ExportManifest.Author,
            SettingsFromExport(parts.ExportManifest),
            copyId,
            token);
        
        var importedArtifacts = await this.MaterializeImportedArtifactsAsync(
            manifest.BriefingId,
            parts,
            projectLockHeld: false,
            token: token);
        
        var data = RemoveProtectedData(parts.Data);
        var assets = VisualBriefingData.ExtractAssets(parts.Data);
        var result = await this.AddRevisionAsync(new(
            manifest.BriefingId,
            null,
            VisualBriefingEditMode.INITIAL,
            string.Empty,
            data,
            parts.TemplateHtml,
            parts.Css,
            string.Empty,
            "Imported copy",
            importedArtifacts.Content.ArtifactId,
            importedArtifacts.Presentation.ArtifactId,
            ModelContributions:
            [
                new(VisualBriefingModelRole.CONTENT, importedArtifacts.Content.Model),
                new(VisualBriefingModelRole.DESIGN, importedArtifacts.Presentation.Model),
            ],
            EmbeddedAssets: assets,
            AssetPlan: importedArtifacts.Content.AssetPlan), token);
        
        return result is { Success: true, Version: not null }
            ? new(true, manifest.BriefingId, result.Version.RevisionId, false, false, string.Empty)
            : new(false, manifest.BriefingId, Guid.Empty, false, false, result.Issue);
    }

    /// <summary>
    /// Materializes local immutable intermediate artifacts from a validated imported standalone version.
    /// </summary>
    /// <param name="briefingId">The local briefing identifier.</param>
    /// <param name="parts">The validated standalone artifact parts.</param>
    /// <param name="projectLockHeld">Whether the caller already owns the project lock.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The local content and presentation artifacts.</returns>
    private async Task<(VisualBriefingContentArtifact Content, VisualBriefingPresentationArtifact Presentation)>
        MaterializeImportedArtifactsAsync(
            Guid briefingId,
            VisualBriefingArtifactParts parts,
            bool projectLockHeld,
            CancellationToken token)
    {
        var businessData = VisualBriefingData.RemoveProtectedData(parts.Data);
        var assetPlan = VisualBriefingData.ExtractAssetPlan(parts.Data);
        var structuralSignature = VisualBriefingHashing.StructuralSignature(businessData);
        
        List<VisualBriefingSourceCoverage> coverage = [];
        var importedSlots = new List<VisualBriefingSlotValue>
        {
            new() { SlotId = "imported_data", Value = businessData },
        };
        
        // Section order and count must match ReadContentArtifactAsync exactly:
        var contentHash = VisualBriefingHashing.ComputeSections(
            JsonSerializer.Serialize(importedSlots, VisualBriefingJson.Compact),
            "[]",
            "[]",
            "[]",
            "{}",
            "{}",
            "{}",
            "Reset",
            JsonSerializer.Serialize<Dictionary<string, string>?>(null, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(coverage, VisualBriefingJson.Compact),
            JsonSerializer.Serialize(assetPlan, VisualBriefingJson.Compact),
            structuralSignature);
        
        var content = new VisualBriefingContentArtifact
        {
            ArtifactId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PayloadHash = contentHash,
            Data = businessData,
            Slots = importedSlots,
            ResetLabel = "Reset",
            SourceCoverage = coverage,
            AssetPlan = assetPlan,
            StructuralSignature = structuralSignature,
            Model = "Imported artifact",
        };
        
        var importedLayout = new VisualBriefingLayoutNode
        {
            NodeId = "imported",
            Kind = VisualBriefingLayoutNodeKind.SECTION,
            Children =
            [
                new()
                {
                    NodeId = "imported_component_node",
                    Kind = VisualBriefingLayoutNodeKind.COMPONENT,
                    ComponentId = "imported_component",
                },
            ],
        };
        
        var importedTokens = new VisualBriefingDesignTokens();
        var templateHash = VisualBriefingHashing.Compute(parts.TemplateHtml);
        var cssHash = VisualBriefingHashing.Compute(parts.Css);
        var presentation = new VisualBriefingPresentationArtifact
        {
            ArtifactId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PayloadHash = VisualBriefingHashing.ComputeSections(
                JsonSerializer.Serialize(importedLayout, VisualBriefingJson.Compact),
                JsonSerializer.Serialize(importedTokens, VisualBriefingJson.Compact),
                templateHash,
                cssHash),
            Layout = importedLayout,
            Tokens = importedTokens,
            TemplateHtml = parts.TemplateHtml,
            Css = parts.Css,
            TemplateHash = templateHash,
            CssHash = cssHash,
            Model = "Imported artifact",
        };
        
        if (projectLockHeld)
        {
            await this.WriteContentArtifactWithoutLockAsync(briefingId, content, token);
            await this.WritePresentationArtifactWithoutLockAsync(briefingId, presentation, token);
        }
        else
        {
            await this.WriteContentArtifactAsync(briefingId, content, token);
            await this.WritePresentationArtifactAsync(briefingId, presentation, token);
        }
        
        return (content, presentation);
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
    /// Defines <c>ReconcileAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task ReconcileAsync(Guid briefingId, CancellationToken token)
    {
        var gate = this.GetLock(briefingId);
        await gate.WaitAsync(token);
        try
        {
            var manifest = await this.LoadWithoutInitializeAsync(briefingId, token);
            if (manifest is null)
                return;
            
            Directory.CreateDirectory(this.VersionsDirectory(briefingId));
            Directory.CreateDirectory(this.TranscriptsDirectory(briefingId));
            Directory.CreateDirectory(this.EvidenceArtifactsDirectory(briefingId));
            Directory.CreateDirectory(this.PlanArtifactsDirectory(briefingId));
            Directory.CreateDirectory(this.ContentArtifactsDirectory(briefingId));
            Directory.CreateDirectory(this.PresentationArtifactsDirectory(briefingId));
            Directory.CreateDirectory(this.BuildsDirectory(briefingId));
            
            var builds = await this.LoadBuildsWithoutLockAsync(briefingId, token);
            foreach (var committedBuild in builds.Where(build =>
                         build.Status is VisualBriefingBuildStatus.ACTIVE &&
                         build.RevisionId is not null &&
                         manifest.Versions.Any(version =>
                             version.RevisionId == build.RevisionId &&
                             version.BuildId == build.BuildId)))
            {
                var committedVersion = manifest.Versions.Single(version =>
                    version.RevisionId == committedBuild.RevisionId &&
                    version.BuildId == committedBuild.BuildId);
                
                foreach (var stageName in new[]
                         {
                             VisualBriefingBuildStage.ASSEMBLY,
                             VisualBriefingBuildStage.COMMIT,
                         })
                {
                    var stage = committedBuild.Stages.FirstOrDefault(item => item.Stage == stageName);
                    if (stage is null)
                        continue;
                    stage.Status = VisualBriefingBuildStageStatus.COMPLETED;
                    stage.FinishedAtUtc ??= committedVersion.CreatedAtUtc;
                    stage.OutputHash = committedVersion.PayloadHash;
                    stage.Failure = null;
                }
                
                committedBuild.CommittedRevisionId = committedVersion.RevisionId;
                committedBuild.Status = VisualBriefingBuildStatus.COMPLETED;
                committedBuild.Failure = null;
                committedBuild.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await this.StoreBuildAtomicAsync(committedBuild, token);
            }
            
            foreach (var interruptedBuild in builds.Where(build => build.Status is VisualBriefingBuildStatus.ACTIVE))
            {
                var interruptedStages = interruptedBuild.Stages.Where(stage =>
                    stage.Status is VisualBriefingBuildStageStatus.RUNNING).ToArray();
                
                if (interruptedStages.Length == 0)
                {
                    var nextStage = interruptedBuild.Stages
                        .OrderBy(stage => stage.Stage)
                        .FirstOrDefault(stage => stage.Status is VisualBriefingBuildStageStatus.NOT_STARTED);
                    
                    if (nextStage is not null)
                        interruptedStages = [nextStage];
                }
                
                VisualBriefingFailure? interruptedFailure = null;
                foreach (var interruptedStage in interruptedStages)
                {
                    interruptedStage.Status = VisualBriefingBuildStageStatus.FAILED;
                    interruptedStage.FinishedAtUtc = DateTimeOffset.UtcNow;
                    interruptedFailure = new()
                    {
                        Code = VisualBriefingFailureCode.BUILD_INTERRUPTED,
                        Stage = interruptedStage.Stage,
                        UserMessage = "The interrupted visual briefing build can be resumed.",
                        TechnicalDetails = "The app stopped before this stage completed.",
                    };
                    
                    interruptedStage.Failure = interruptedFailure;
                }
                
                if (interruptedFailure is null)
                    continue;
                
                interruptedBuild.Status = VisualBriefingBuildStatus.FAILED;
                interruptedBuild.Failure = interruptedFailure;
                interruptedBuild.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await this.StoreBuildAtomicAsync(interruptedBuild, token);
            }

            var changed = manifest.Versions.RemoveAll(version =>
                !File.Exists(this.VersionPath(briefingId, version))) > 0;
            
            var knownFiles = manifest.Versions.Select(version => version.FileName).ToHashSet(StringComparer.Ordinal);
            foreach (var versionPath in Directory.EnumerateFiles(this.VersionsDirectory(briefingId), "*.html"))
            {
                token.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(versionPath);
                if (knownFiles.Contains(fileName))
                    continue;

                var html = await File.ReadAllTextAsync(versionPath, token);
                if (!artifactService.TryParse(html, out var parts, out _))
                    continue;

                var hashes = ComputeSectionHashes(parts);
                var versionNumber = ParseVersionNumber(fileName);
                if (versionNumber <= 0 ||
                    !string.Equals(fileName, $"{versionNumber:000000}-{parts.ExportManifest.RevisionId:D}.html", StringComparison.Ordinal) ||
                    manifest.Versions.Any(version => version.RevisionId == parts.ExportManifest.RevisionId ||
                                                     version.VersionNumber == versionNumber))
                    continue;

                var matchingBuild = builds.FirstOrDefault(build => build.RevisionId == parts.ExportManifest.RevisionId);
                manifest.Versions.Add(new()
                {
                    VersionNumber = versionNumber,
                    RevisionId = parts.ExportManifest.RevisionId,
                    ParentRevisionId = parts.ExportManifest.ParentRevisionId,
                    CreatedAtUtc = parts.ExportManifest.CreatedAtUtc,
                    EditMode = matchingBuild?.Mode ?? VisualBriefingEditMode.IMPORT,
                    Instruction = matchingBuild?.Instruction ?? string.Empty,
                    PayloadHash = parts.PayloadHash,
                    Origin = "Recovered from disk",
                    FileName = fileName,
                    DataHash = hashes.DataHash,
                    AssetHash = hashes.AssetHash,
                    TemplateHash = hashes.TemplateHash,
                    CssHash = hashes.CssHash,
                    RuntimeHash = hashes.RuntimeHash,
                    EvidenceArtifactId = matchingBuild?.EvidenceArtifactId,
                    PlanArtifactId = matchingBuild?.PlanArtifactId,
                    ContentArtifactId = matchingBuild?.ContentArtifactId,
                    PresentationArtifactId = matchingBuild?.PresentationArtifactId,
                    BuildId = matchingBuild?.BuildId,
                    OperationId = matchingBuild?.OperationId,
                    ModelContributions = BuildRecoveredContributions(matchingBuild),
                });
                
                if (matchingBuild is not null)
                {
                    matchingBuild.CommittedRevisionId = parts.ExportManifest.RevisionId;
                    matchingBuild.Status = VisualBriefingBuildStatus.COMPLETED;
                    matchingBuild.Failure = null;
                    matchingBuild.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    await this.StoreBuildAtomicAsync(matchingBuild, token);
                }
                
                changed = true;
            }

            if (changed)
            {
                manifest.Versions = manifest.Versions.OrderBy(version => version.VersionNumber).ToList();
                manifest.ModifiedAtUtc = DateTimeOffset.UtcNow;
                await this.StoreManifestAtomicAsync(manifest, token);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            logger.LogError(
                new EventId((int)VisualBriefingLogEventId.STORE_RECOVERY, VisualBriefingLogEventId.STORE_RECOVERY.ToString()),
                "Could not reconcile visual briefing. BriefingId={BriefingId} ExceptionType={ExceptionType}",
                briefingId,
                exception.GetType().Name);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Defines <c>MutateManifestAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task MutateManifestAsync(Guid briefingId, Action<VisualBriefingManifest> mutation, CancellationToken token)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(briefingId);
        await gate.WaitAsync(token);
        
        try
        {
            var manifest = await this.LoadRequiredWithoutInitializeAsync(briefingId, token);
            mutation(manifest);
            await this.StoreManifestAtomicAsync(manifest, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Defines <c>LoadRequiredWithoutInitializeAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task<VisualBriefingManifest> LoadRequiredWithoutInitializeAsync(
        Guid briefingId,
        CancellationToken token)
    {
        var path = this.ManifestPath(briefingId);
        if (!File.Exists(path))
            throw new FileNotFoundException("The visual briefing does not exist.", path);

        return await this.LoadWithoutInitializeAsync(briefingId, token)
            ?? throw new InvalidDataException("The visual briefing manifest is invalid.");
    }

    /// <summary>
    /// Defines <c>LoadWithoutInitializeAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task<VisualBriefingManifest?> LoadWithoutInitializeAsync(Guid briefingId, CancellationToken token)
    {
        var path = this.ManifestPath(briefingId);
        if (!File.Exists(path))
            return null;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
        var manifest = await JsonSerializer.DeserializeAsync<VisualBriefingManifest>(stream, JSON_OPTIONS, token);
        
        return manifest is not null && IsValidManifest(manifest, briefingId) ? manifest : null;
    }

    /// <summary>
    /// Defines <c>StoreManifestAtomicAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task StoreManifestAtomicAsync(VisualBriefingManifest manifest, CancellationToken token)
    {
        var json = JsonSerializer.Serialize(manifest, JSON_OPTIONS);
        await WriteTextAtomicAsync(this.ManifestPath(manifest.BriefingId), json, token);
    }

    /// <summary>
    /// Writes one build record atomically.
    /// </summary>
    /// <param name="build">The build record.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="overwrite">Whether an existing record may be replaced.</param>
    private async Task StoreBuildAtomicAsync(
        VisualBriefingBuildRecord build,
        CancellationToken token,
        bool overwrite = true)
    {
        if (build.BuildVersion != VisualBriefingVersions.BUILD ||
            build.BuildId == Guid.Empty ||
            build.OperationId == Guid.Empty ||
            build.BriefingId == Guid.Empty)
            throw new InvalidDataException("The visual briefing build record is invalid.");

        var json = JsonSerializer.Serialize(build, JSON_OPTIONS);
        await WriteTextAtomicAsync(this.BuildPath(build.BriefingId, build.BuildId), json, token, overwrite);
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
            var build = await this.LoadBuildWithoutLockAsync(path, briefingId, token);
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
    private async Task<VisualBriefingBuildRecord?> LoadBuildWithoutLockAsync(
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

    /// <summary>
    /// Reads a JSON file while treating malformed persisted diagnostics as unavailable.
    /// </summary>
    /// <typeparam name="T">The JSON model type.</typeparam>
    /// <param name="path">The file path.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The parsed value, or <see langword="null"/>.</returns>
    private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken token)
        where T : class
    {
        if (!File.Exists(path))
            return null;
        
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            return await JsonSerializer.DeserializeAsync<T>(stream, JSON_OPTIONS, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes an immutable intermediate artifact without replacing an existing file.
    /// </summary>
    /// <param name="path">The artifact path.</param>
    /// <param name="json">The serialized artifact.</param>
    /// <param name="token">The cancellation token.</param>
    private static async Task WriteImmutableArtifactAsync(
        string path,
        string json,
        CancellationToken token)
    {
        await WriteTextAtomicAsync(path, json, token, overwrite: false);
    }

    /// <summary>
    /// Reconstructs footer model roles for an orphaned committed version.
    /// </summary>
    /// <param name="build">The matching build record.</param>
    /// <returns>The recovered contributions.</returns>
    private static List<VisualBriefingModelContribution> BuildRecoveredContributions(
        VisualBriefingBuildRecord? build)
    {
        if (build is null || string.IsNullOrWhiteSpace(build.Model))
            return [];

        List<VisualBriefingModelContribution> contributions = [];
        if (build.EvidenceArtifactId is not null)
            contributions.Add(new(VisualBriefingModelRole.EVIDENCE, build.Model));
        
        if (build.PlanArtifactId is not null)
            contributions.Add(new(VisualBriefingModelRole.PLAN, build.Model));
        
        if (build.ContentArtifactId is not null)
            contributions.Add(new(VisualBriefingModelRole.CONTENT, build.Model));
        
        if (build.PresentationArtifactId is not null)
            contributions.Add(new(VisualBriefingModelRole.DESIGN, build.Model));
        
        return contributions;
    }

    /// <summary>
    /// Defines <c>LoadSelectionAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task LoadSelectionAsync(CancellationToken token)
    {
        var path = this.SelectionPath();
        if (!File.Exists(path))
            return;

        try
        {
            var serialized = await File.ReadAllTextAsync(path, token);
            var selected = JsonSerializer.Deserialize<Guid?>(serialized);
            this.LastSelectedBriefingId = selected is not null &&
                                          Directory.Exists(this.BriefingDirectory(selected.Value))
                ? selected
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(
                new EventId((int)VisualBriefingLogEventId.STORE_REJECTED, VisualBriefingLogEventId.STORE_REJECTED.ToString()),
                "Could not restore the last selected visual briefing. ExceptionType={ExceptionType}",
                exception.GetType().Name);
            this.LastSelectedBriefingId = null;
        }
    }

    /// <summary>
    /// Defines <c>RefreshSourceStatuses</c> for the visual briefing feature.
    /// </summary>
    private static void RefreshSourceStatuses(VisualBriefingManifest manifest)
    {
        foreach (var source in manifest.Sources)
        {
            if (!File.Exists(source.Path))
            {
                source.Status = VisualBriefingSourceStatus.UNREACHABLE;
                continue;
            }

            var info = new FileInfo(source.Path);
            var changed = info.Length != source.Size || info.LastWriteTimeUtc != source.LastWriteTimeUtc.UtcDateTime;
            source.Status = changed
                ? source.IsMedia ? VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED : VisualBriefingSourceStatus.CHANGED
                : source.IsMedia && source.TranscriptStatus is not VisualBriefingTranscriptStatus.CURRENT
                    ? VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED
                    : VisualBriefingSourceStatus.UNCHANGED;
        }
    }

    /// <summary>
    /// Defines <c>MergeSources</c> for the visual briefing feature.
    /// </summary>
    private static List<VisualBriefingSource> MergeSources(
        IReadOnlyCollection<VisualBriefingSource> existing,
        IEnumerable<(string Path, VisualBriefingSourceKind Kind)> updated)
    {
        List<VisualBriefingSource> result = [];
        foreach (var (path, kind) in updated.DistinctBy(item => Path.GetFullPath(item.Path), PathComparer()))
        {
            var fullPath = Path.GetFullPath(path);
            if (kind is VisualBriefingSourceKind.VISUAL_ASSET &&
                !FileTypes.IsAllowedPath(fullPath, FileTypes.VISUAL_BRIEFING_IMAGE))
                throw new InvalidDataException("Visual assets must be PNG, JPEG, or WebP files.");

            var source = existing.FirstOrDefault(candidate =>
                candidate.Kind == kind && PathComparer().Equals(Path.GetFullPath(candidate.Path), fullPath));
            
            if (!File.Exists(fullPath))
            {
                if (source is not null)
                    result.Add(source);
                
                continue;
            }

            if (!IsSupportedSourcePath(fullPath))
                throw new InvalidDataException($"The source file type '{Path.GetExtension(fullPath)}' is not supported.");

            if (source is null)
            {
                source = new VisualBriefingSource
                {
                    SourceId = Guid.NewGuid(),
                    Kind = kind,
                    AssetId = kind is VisualBriefingSourceKind.VISUAL_ASSET
                        ? NextAssetId(existing.Concat(result))
                        : string.Empty,
                    IsMedia = FileTypes.IsAllowedPath(fullPath, FileTypes.AUDIO, FileTypes.VIDEO),
                };
                
                ApplyFileSnapshot(source, fullPath);
                source.TranscriptStatus = source.IsMedia
                    ? VisualBriefingTranscriptStatus.MISSING
                    : VisualBriefingTranscriptStatus.NOT_REQUIRED;
            }

            result.Add(source);
        }

        return result;
    }

    /// <summary>
    /// Picks the asset handle for a new visual asset. Asset IDs reach the model, which cannot
    /// reproduce opaque identifiers reliably, so they stay short. The smallest free number is taken
    /// instead of renumbering, so removing one asset never changes the handle of another.
    /// </summary>
    /// <param name="sources">The sources that already carry an asset handle.</param>
    /// <returns>The new asset handle.</returns>
    private static string NextAssetId(IEnumerable<VisualBriefingSource> sources)
    {
        var used = sources
            .Select(source => source.AssetId)
            .Where(assetId => !string.IsNullOrWhiteSpace(assetId))
            .ToHashSet(StringComparer.Ordinal);
        var number = 1;
        while (used.Contains($"a{number}"))
            number++;

        return $"a{number}";
    }

    /// <summary>
    /// Defines <c>ApplyFileSnapshot</c> for the visual briefing feature.
    /// </summary>
    private static void ApplyFileSnapshot(VisualBriefingSource source, string path)
    {
        var info = new FileInfo(path);
        source.Path = info.FullName;
        source.Size = info.Length;
        source.LastWriteTimeUtc = info.LastWriteTimeUtc;
        source.IsMedia = FileTypes.IsAllowedPath(info.FullName, FileTypes.AUDIO, FileTypes.VIDEO);
        source.Status = VisualBriefingSourceStatus.UNCHANGED;
    }

    /// <summary>
    /// Defines <c>SettingsFromExport</c> for the visual briefing feature.
    /// </summary>
    private static VisualBriefingLocalSettings SettingsFromExport(VisualBriefingExportManifest export) => new()
    {
        TargetLanguage = export.TargetLanguage,
        CustomTargetLanguage = export.CustomTargetLanguage,
        AudienceProfile = export.AudienceProfile,
        AudienceAgeGroup = export.AudienceAgeGroup,
        AudienceOrganizationalLevel = export.AudienceOrganizationalLevel,
        AudienceExpertise = export.AudienceExpertise,
        ShowSourceReferences = export.ShowSourceReferences,
        ProtectionLevel = export.ProtectionLevel,
        CustomProtectionLevel = export.CustomProtectionLevel,
    };

    /// <summary>
    /// Defines <c>RemoveProtectedData</c> for the visual briefing feature.
    /// </summary>
    private static JsonElement RemoveProtectedData(JsonElement data)
        => VisualBriefingData.RemoveProtectedData(data);

    /// <summary>
    /// Defines <c>ComputeSectionHashes</c> for the visual briefing feature.
    /// </summary>
    private static SectionHashes ComputeSectionHashes(VisualBriefingArtifactParts parts)
    {
        var businessData = VisualBriefingHashing.CanonicalJson(VisualBriefingData.RemoveProtectedData(parts.Data));
        var assets = JsonSerializer.Serialize(
            VisualBriefingData.ExtractAssets(parts.Data),
            VisualBriefingJson.Compact);
        return new(
            VisualBriefingHashing.Compute(businessData),
            VisualBriefingHashing.Compute(assets),
            VisualBriefingHashing.Compute(parts.TemplateHtml),
            VisualBriefingHashing.Compute(parts.Css),
            VisualBriefingHashing.Compute(parts.RuntimeScript + (parts.EChartsScript ?? string.Empty)));
    }

    /// <summary>
    /// Defines <c>WriteTextAtomicAsync</c> for the visual briefing feature.
    /// </summary>
    private static async Task WriteTextAtomicAsync(
        string targetPath,
        string content,
        CancellationToken token,
        bool overwrite = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = $"{targetPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(false), token);
            await using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4_096, true))
                await stream.FlushAsync(token);
            File.Move(temporaryPath, targetPath, overwrite);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    /// <summary>
    /// Defines <c>TryDeleteFile</c> for the visual briefing feature.
    /// </summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Startup and rollback cleanup are best effort.
        }
    }

    /// <summary>
    /// Defines <c>NamesEqual</c> for the visual briefing feature.
    /// </summary>
    private static bool NamesEqual(string first, string second) =>
        string.Equals(NormalizeName(first), NormalizeName(second), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Defines <c>NormalizeName</c> for the visual briefing feature.
    /// </summary>
    private static string NormalizeName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Defines <c>ParseVersionNumber</c> for the visual briefing feature.
    /// </summary>
    private static int ParseVersionNumber(string fileName) =>
        fileName.Length >= 6 && int.TryParse(fileName.AsSpan(0, 6), out var value) ? value : 0;

    /// <summary>
    /// Defines <c>NextVersionNumber</c> for the visual briefing feature.
    /// </summary>
    private int NextVersionNumber(VisualBriefingManifest manifest)
    {
        var manifestMaximum = manifest.Versions.Select(version => version.VersionNumber).DefaultIfEmpty().Max();
        var diskMaximum = Directory.EnumerateFiles(this.VersionsDirectory(manifest.BriefingId), "*.html")
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null)
            .Select(fileName => ParseVersionNumber(fileName!))
            .DefaultIfEmpty()
            .Max();
        return Math.Max(manifestMaximum, diskMaximum) + 1;
    }

    /// <summary>
    /// Defines <c>IsValidManifest</c> for the visual briefing feature.
    /// </summary>
    private static bool IsValidManifest(VisualBriefingManifest manifest, Guid expectedBriefingId)
    {
        if (manifest.ManifestVersion is < 1 or > VisualBriefingVersions.MANIFEST ||
            manifest.BriefingId != expectedBriefingId ||
            manifest.BriefingId == Guid.Empty ||
            string.IsNullOrWhiteSpace(manifest.Name) ||
            IsNull(manifest.Settings) ||
            IsNull(manifest.Sources) ||
            IsNull(manifest.Versions) ||
            manifest.Sources.Any(source =>
                source.SourceId == Guid.Empty ||
                string.IsNullOrWhiteSpace(source.Path) ||
                !Path.IsPathFullyQualified(source.Path) ||
                source.Kind is VisualBriefingSourceKind.VISUAL_ASSET &&
                (string.IsNullOrWhiteSpace(source.AssetId) ||
                 !IsValidAssetId(source.AssetId))) ||
            manifest.Sources.Select(source => source.SourceId).Distinct().Count() != manifest.Sources.Count ||
            manifest.Sources.Where(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET)
                .Select(source => source.AssetId).Distinct(StringComparer.Ordinal).Count() !=
            manifest.Sources.Count(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET))
            return false;

        foreach (var version in manifest.Versions)
        {
            if (version.VersionNumber <= 0 ||
                version.RevisionId == Guid.Empty ||
                string.IsNullOrWhiteSpace(version.PayloadHash) ||
                version.PayloadHash.Length != 64 ||
                !version.PayloadHash.All(Uri.IsHexDigit) ||
                !string.Equals(
                    version.FileName,
                    $"{version.VersionNumber:000000}-{version.RevisionId:D}.html",
                    StringComparison.Ordinal))
                return false;
        }

        return manifest.Versions.Select(version => version.VersionNumber).Distinct().Count() == manifest.Versions.Count &&
               manifest.Versions.Select(version => version.RevisionId).Distinct().Count() == manifest.Versions.Count;
    }

    /// <summary>
    /// Returns whether an asset identifier is safe for JSON paths, bindings, and HTML attributes.
    /// </summary>
    /// <param name="assetId">The identifier to validate.</param>
    /// <returns><see langword="true"/> for a canonical asset identifier.</returns>
    private static bool IsValidAssetId(string assetId) =>
        assetId.StartsWith('a') &&
        assetId.Length is > 1 and <= 16 &&
        assetId[1..].All(char.IsAsciiDigit);

    /// <summary>
    /// Defines <c>PathComparer</c> for the visual briefing feature.
    /// </summary>
    private static StringComparer PathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// Defines <c>IsSupportedSourcePath</c> for the visual briefing feature.
    /// </summary>
    private static bool IsSupportedSourcePath(string path) =>
        FileAttachment.FromPath(path).IsValid ||
        FileTypes.IsAllowedPath(path, FileTypes.AUDIO, FileTypes.VIDEO);

    /// <summary>
    /// Defines <c>T</c> for the visual briefing feature.
    /// </summary>
    private static bool IsNull<T>(T? value) => value is null;

    /// <summary>
    /// Defines <c>GetLock</c> for the visual briefing feature.
    /// </summary>
    private SemaphoreSlim GetLock(Guid briefingId) => this.briefingLocks.GetOrAdd(briefingId, _ => new(1, 1));

    /// <summary>
    /// Defines <c>BriefingDirectory</c> for the visual briefing feature.
    /// </summary>
    private string BriefingDirectory(Guid briefingId) => Path.Combine(this.RootDirectory, briefingId.ToString("D"));

    /// <summary>
    /// Defines <c>ManifestPath</c> for the visual briefing feature.
    /// </summary>
    private string ManifestPath(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), MANIFEST_FILE_NAME);

    /// <summary>
    /// Defines <c>SelectionPath</c> for the visual briefing feature.
    /// </summary>
    private string SelectionPath() => Path.Combine(this.RootDirectory, SELECTION_FILE_NAME);

    /// <summary>
    /// Defines <c>VersionsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string VersionsDirectory(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), VERSIONS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>TranscriptsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string TranscriptsDirectory(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), TRANSCRIPTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>ArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string ArtifactsDirectory(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>EvidenceArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string EvidenceArtifactsDirectory(Guid briefingId) =>
        Path.Combine(this.ArtifactsDirectory(briefingId), EVIDENCE_ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>PlanArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string PlanArtifactsDirectory(Guid briefingId) =>
        Path.Combine(this.ArtifactsDirectory(briefingId), PLAN_ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>ContentArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string ContentArtifactsDirectory(Guid briefingId) =>
        Path.Combine(this.ArtifactsDirectory(briefingId), CONTENT_ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>PresentationArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string PresentationArtifactsDirectory(Guid briefingId) =>
        Path.Combine(this.ArtifactsDirectory(briefingId), PRESENTATION_ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>BuildsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string BuildsDirectory(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), BUILDS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>EvidenceArtifactPath</c> for the visual briefing feature.
    /// </summary>
    private string EvidenceArtifactPath(Guid briefingId, Guid artifactId) =>
        Path.Combine(this.EvidenceArtifactsDirectory(briefingId), $"{artifactId:D}.json");

    /// <summary>
    /// Defines <c>PlanArtifactPath</c> for the visual briefing feature.
    /// </summary>
    private string PlanArtifactPath(Guid briefingId, Guid artifactId) =>
        Path.Combine(this.PlanArtifactsDirectory(briefingId), $"{artifactId:D}.json");

    /// <summary>
    /// Defines <c>ContentArtifactPath</c> for the visual briefing feature.
    /// </summary>
    private string ContentArtifactPath(Guid briefingId, Guid artifactId) =>
        Path.Combine(this.ContentArtifactsDirectory(briefingId), $"{artifactId:D}.json");

    /// <summary>
    /// Defines <c>PresentationArtifactPath</c> for the visual briefing feature.
    /// </summary>
    private string PresentationArtifactPath(Guid briefingId, Guid artifactId) =>
        Path.Combine(this.PresentationArtifactsDirectory(briefingId), $"{artifactId:D}.json");

    /// <summary>
    /// Defines <c>BuildPath</c> for the visual briefing feature.
    /// </summary>
    private string BuildPath(Guid briefingId, Guid buildId) =>
        Path.Combine(this.BuildsDirectory(briefingId), $"{buildId:D}.json");

    /// <summary>
    /// Defines <c>TranscriptPath</c> for the visual briefing feature.
    /// </summary>
    private string TranscriptPath(Guid briefingId, Guid sourceId) => Path.Combine(this.TranscriptsDirectory(briefingId), $"{sourceId:D}.md");

    /// <summary>
    /// Defines <c>VersionPath</c> for the visual briefing feature.
    /// </summary>
    private string VersionPath(Guid briefingId, VisualBriefingVersion version) => Path.Combine(this.VersionsDirectory(briefingId), version.FileName);

    /// <summary>
    /// Defines <c>SectionHashes</c> for the visual briefing feature.
    /// </summary>
    private sealed record SectionHashes(string DataHash, string AssetHash, string TemplateHash, string CssHash, string RuntimeHash);
}
