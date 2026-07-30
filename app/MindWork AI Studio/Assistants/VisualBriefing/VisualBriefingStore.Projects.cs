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
    /// Defines <c>NamesEqual</c> for the visual briefing feature.
    /// </summary>
    private static bool NamesEqual(string first, string second) => string.Equals(NormalizeName(first), NormalizeName(second), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Defines <c>NormalizeName</c> for the visual briefing feature.
    /// </summary>
    private static string NormalizeName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}