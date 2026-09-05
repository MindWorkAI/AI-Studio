using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingStore
{
    /// <summary>
    /// Defines <c>LastSelectedBriefingId</c> for the visual briefing feature.
    /// </summary>
    public Guid? LastSelectedBriefingId { get; private set; }

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
            await WriteTextAtomicAsync(this.SelectionPath(), JsonSerializer.Serialize<Guid?>(briefingId), overwrite: true, token);
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
            await WriteTextAtomicAsync(this.SelectionPath(), JsonSerializer.Serialize<Guid?>(null), overwrite: true, token);
        }
        finally
        {
            this.selectionLock.Release();
        }
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
    /// Defines <c>InitializeAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task InitializeAsync(CancellationToken token = default)
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
        var projects = await this.ListProjectsAsync(token);
        return [.. projects.Where(project => project.IsAvailable).Select(project => project.Manifest!)];
    }

    /// <summary>
    /// Lists every project directory, including projects whose manifests cannot be opened.
    /// </summary>
    internal async Task<IReadOnlyList<VisualBriefingProjectEntry>> ListProjectsAsync(CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        List<VisualBriefingProjectEntry> projects = [];
        foreach (var directory in Directory.EnumerateDirectories(this.RootDirectory))
        {
            token.ThrowIfCancellationRequested();
            if (!Guid.TryParse(Path.GetFileName(directory), out var briefingId))
                continue;

            projects.Add(await this.LoadProjectEntryAsync(briefingId, directory, token));
        }

        return projects.OrderByDescending(project => project.ModifiedAtUtc).ToArray();
    }

    /// <summary>
    /// Gets the exact project directory without interpreting or modifying its contents.
    /// </summary>
    internal async Task<string?> GetProjectDirectoryPathAsync(Guid briefingId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var path = this.BriefingDirectory(briefingId);
        return Directory.Exists(path) ? path : null;
    }

    /// <summary>
    /// Loads a normal manifest or returns a recovery entry with best-effort display metadata.
    /// </summary>
    private async Task<VisualBriefingProjectEntry> LoadProjectEntryAsync(Guid briefingId, string directory, CancellationToken token)
    {
        var path = this.ManifestPath(briefingId);
        var modifiedAtUtc = ProjectModifiedAtUtc(path, directory);
        if (!File.Exists(path))
            return new(briefingId, string.Empty, modifiedAtUtc, VisualBriefingProjectLoadStatus.UNAVAILABLE, null);

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.LogUnavailableManifest(briefingId, exception);
            return new(briefingId, string.Empty, modifiedAtUtc, VisualBriefingProjectLoadStatus.UNAVAILABLE, null);
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<VisualBriefingManifest>(json, JSON_OPTIONS);
            if (manifest is not null && IsValidManifest(manifest, briefingId))
            {
                RefreshSourceStatuses(manifest);
                return VisualBriefingProjectEntry.FromManifest(manifest);
            }
        }
        catch (JsonException exception)
        {
            this.LogUnavailableManifest(briefingId, exception);
        }

        var (name, persistedModifiedAtUtc, manifestVersion) = ReadProjectMetadata(json);
        var status = manifestVersion is > VisualBriefingVersions.MANIFEST ? VisualBriefingProjectLoadStatus.NEWER_VERSION : VisualBriefingProjectLoadStatus.UNAVAILABLE;
        return new(briefingId, name, persistedModifiedAtUtc ?? modifiedAtUtc, status, null);
    }

    /// <summary>
    /// Reads only non-authoritative display metadata from an otherwise unusable manifest.
    /// </summary>
    private static (string Name, DateTimeOffset? ModifiedAtUtc, int? ManifestVersion) ReadProjectMetadata(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                return (string.Empty, null, null);

            var root = document.RootElement;
            var name = root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind is JsonValueKind.String ? SanitizeProjectName(nameElement.GetString()) : string.Empty;
            DateTimeOffset? modifiedAtUtc = root.TryGetProperty("modifiedAtUtc", out var modifiedElement) && modifiedElement.ValueKind is JsonValueKind.String &&
                                               modifiedElement.TryGetDateTimeOffset(out var parsedModifiedAtUtc) ? parsedModifiedAtUtc : null;
            
            int? manifestVersion = root.TryGetProperty("manifestVersion", out var versionElement) && versionElement.ValueKind is JsonValueKind.Number &&
                                   versionElement.TryGetInt32(out var parsedManifestVersion) ? parsedManifestVersion : null;

            return (name, modifiedAtUtc, manifestVersion);
        }
        catch (JsonException)
        {
            return (string.Empty, null, null);
        }
    }

    /// <summary>
    /// Removes control characters and bounds untrusted recovery-list text.
    /// </summary>
    private static string SanitizeProjectName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var sanitized = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return sanitized.Length <= 200 ? sanitized : sanitized[..200];
    }

    /// <summary>
    /// Gets a stable fallback timestamp from the manifest or project directory.
    /// </summary>
    private static DateTimeOffset ProjectModifiedAtUtc(string manifestPath, string directory)
    {
        try
        {
            var timestamp = File.Exists(manifestPath) ? File.GetLastWriteTimeUtc(manifestPath) : Directory.GetLastWriteTimeUtc(directory);
            return new DateTimeOffset(timestamp);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DateTimeOffset.UnixEpoch;
        }
    }

    /// <summary>
    /// Records why a manifest was exposed through the recovery lane.
    /// </summary>
    private void LogUnavailableManifest(Guid briefingId, Exception exception)
    {
        logger.LogWarning(new EventId((int)VisualBriefingLogEventId.STORE_REJECTED, nameof(VisualBriefingLogEventId.STORE_REJECTED)), exception,
            "Could not load visual briefing manifest. BriefingId={BriefingId} ExceptionType={ExceptionType}", briefingId, exception.GetType().Name);
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
                new EventId((int)VisualBriefingLogEventId.STORE_REJECTED, nameof(VisualBriefingLogEventId.STORE_REJECTED)),
                exception,
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
    public async Task SaveProjectAsync(Guid briefingId, string name, string author, VisualBriefingLocalSettings settings, IEnumerable<(string Path, VisualBriefingSourceKind Kind)> sources, CancellationToken token = default)
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
            this.ForgetLock(briefingId);
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
    private async Task<VisualBriefingManifest> LoadRequiredWithoutInitializeAsync(Guid briefingId, CancellationToken token)
    {
        var path = this.ManifestPath(briefingId);
        if (!File.Exists(path))
            throw new FileNotFoundException("The visual briefing does not exist.", path);

        return await this.LoadWithoutInitializeAsync(briefingId, token) ?? throw new InvalidDataException("The visual briefing manifest is invalid.");
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
        await WriteTextAtomicAsync(this.ManifestPath(manifest.BriefingId), json, overwrite: true, token);
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
                version.SchemaVersion <= 0 ||
                version.IntermediateArtifactVersion < 0 ||
                version.EvidenceContractVersion < 0 ||
                version.PlanContractVersion < 0 ||
                version.ContentContractVersion < 0 ||
                version.DesignContractVersion < 0 ||
                string.IsNullOrWhiteSpace(version.DocumentHash) ||
                version.DocumentHash.Length != 64 ||
                !version.DocumentHash.All(Uri.IsHexDigit) ||
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
    /// Defines <c>NamesEqual</c> for the visual briefing feature.
    /// </summary>
    private static bool NamesEqual(string first, string second) => string.Equals(NormalizeName(first), NormalizeName(second), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Defines <c>NormalizeName</c> for the visual briefing feature.
    /// </summary>
    private static string NormalizeName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}