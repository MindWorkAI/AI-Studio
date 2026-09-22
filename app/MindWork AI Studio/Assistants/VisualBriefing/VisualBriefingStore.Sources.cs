using AIStudio.Chat;
using AIStudio.Tools.Rust;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingStore
{
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
            await WriteTextAtomicAsync(transcriptPath, transcript, overwrite: true, token);
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
    /// Returns whether an asset identifier is safe for JSON paths, bindings, and HTML attributes.
    /// </summary>
    /// <param name="assetId">The identifier to validate.</param>
    /// <returns><see langword="true"/> for a canonical asset identifier.</returns>
    private static bool IsValidAssetId(string assetId) =>
        assetId.StartsWith('a') &&
        assetId.Length is > 1 and <= 16 &&
        assetId[1..].All(char.IsAsciiDigit);

    /// <summary>
    /// Defines <c>IsSupportedSourcePath</c> for the visual briefing feature.
    /// </summary>
    private static bool IsSupportedSourcePath(string path) =>
        FileAttachment.FromPath(path).IsValid ||
        FileTypes.IsAllowedPath(path, FileTypes.AUDIO, FileTypes.VIDEO);
}