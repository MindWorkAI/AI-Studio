using AIStudio.Chat;
using AIStudio.Tools.Services;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Validates, fingerprints, and prepares source material for the content and assembly stages.
/// </summary>
/// <param name="store">The persistent visual briefing store.</param>
/// <param name="rustService">The native service used to process and optimize source files.</param>
/// <param name="logger">The source preparation logger.</param>
internal sealed class VisualBriefingSourcePreparationService(VisualBriefingStore store, RustService rustService, ILogger<VisualBriefingSourcePreparationService> logger)
{
    /// <summary>
    /// Prepares all current sources without persisting embedded asset bytes.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="buildId">The build identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The prepared sources.</returns>
    public async Task<VisualBriefingPreparedSources> PrepareAsync(VisualBriefingManifest manifest, Guid operationId, Guid buildId, CancellationToken token)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"mwai-visual-briefing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        
        try
        {
            List<FileAttachment> attachments = [];
            Dictionary<Guid, string> transcripts = [];
            Dictionary<string, PreparedVisualBriefingAsset> assets = new(StringComparer.Ordinal);
            List<string> fingerprints = [];
            long totalBytes = 0;
            
            foreach (var source in manifest.Sources.OrderBy(source => source.SourceId))
            {
                token.ThrowIfCancellationRequested();
                if (!File.Exists(source.Path))
                    throw new VisualBriefingBuildException(VisualBriefingFailureCode.SOURCE_UNREACHABLE, VisualBriefingBuildStage.SOURCE_PREPARATION, "A briefing source is no longer reachable.", "A source failed the reachability check.");

                var info = new FileInfo(source.Path);
                totalBytes += info.Length;
                
                var sourceHash = await VisualBriefingHashing.ComputeFileAsync(source.Path, token);
                var transcriptHash = string.Empty;
                
                if (source.IsMedia)
                {
                    var transcript = await store.ReadTranscriptAsync(manifest.BriefingId, source.SourceId, token);
                    if (string.IsNullOrWhiteSpace(transcript) || source.TranscriptStatus is not VisualBriefingTranscriptStatus.CURRENT)
                        throw new VisualBriefingBuildException(VisualBriefingFailureCode.TRANSCRIPT_UNAVAILABLE, VisualBriefingBuildStage.SOURCE_PREPARATION, "A media transcript is missing or outdated.", $"Transcript status for source {source.SourceId:D} is {source.TranscriptStatus}.");

                    transcripts[source.SourceId] = transcript;
                    transcriptHash = VisualBriefingHashing.Compute(transcript);
                }
                else if (source.Kind is VisualBriefingSourceKind.VISUAL_ASSET)
                {
                    var optimized = await rustService.PrepareImageAsync(source.Path, manifest.Settings.OptimizeImages, token);
                    var extension = optimized.MimeType switch
                    {
                        "image/jpeg" => ".jpg",
                        "image/png" => ".png",
                        "image/webp" => ".webp",
                        
                        _ => throw new VisualBriefingBuildException(VisualBriefingFailureCode.SOURCE_PREPARATION_FAILED, VisualBriefingBuildStage.SOURCE_PREPARATION, "A visual asset has an unsupported image format.", "The image optimizer returned an unsupported MIME type."),
                    };
                    
                    var preparedPath = Path.Combine(temporaryDirectory, $"{source.AssetId}{extension}");
                    await File.WriteAllBytesAsync(preparedPath, DecodeDataUrl(optimized.DataUrl), token);
                    attachments.Add(FileAttachment.FromPath(preparedPath));
                    assets[source.AssetId] = new(source.AssetId, optimized.DataUrl, optimized.Width, optimized.Height);
                }
                else
                {
                    attachments.Add(FileAttachment.FromPath(source.Path));
                }

                fingerprints.Add(string.Join('\u001f', source.SourceId, source.Kind, source.AssetId, sourceHash, transcriptHash));
            }

            var fingerprint = VisualBriefingHashing.ComputeSections([manifest.Settings.OptimizeImages.ToString(), .. fingerprints]);
            logger.LogInformation(Event(VisualBriefingLogEventId.SOURCE_PREPARATION_FINISHED), "Visual briefing source preparation finished. OperationId={OperationId} BuildId={BuildId} SourceCount={SourceCount} AssetCount={AssetCount} TotalBytes={TotalBytes} SourceFingerprint={SourceFingerprint}", operationId, buildId, manifest.Sources.Count, assets.Count, totalBytes, fingerprint);
            
            return new()
            {
                TemporaryDirectory = temporaryDirectory,
                Attachments = attachments,
                Transcripts = transcripts,
                Assets = assets,
                SourceFingerprint = fingerprint,
            };
        }
        catch (OperationCanceledException)
        {
            DeleteTemporaryDirectory(temporaryDirectory);
            throw;
        }
        catch (VisualBriefingBuildException)
        {
            DeleteTemporaryDirectory(temporaryDirectory);
            throw;
        }
        catch (Exception exception)
        {
            DeleteTemporaryDirectory(temporaryDirectory);
            logger.LogWarning(Event(VisualBriefingLogEventId.SOURCE_PREPARATION_REJECTED), "Visual briefing source preparation failed. OperationId={OperationId} BuildId={BuildId} ExceptionType={ExceptionType}", operationId, buildId, exception.GetType().Name);
            throw new VisualBriefingBuildException(VisualBriefingFailureCode.SOURCE_PREPARATION_FAILED, VisualBriefingBuildStage.SOURCE_PREPARATION, "The briefing sources could not be prepared.", $"ExceptionType={exception.GetType().Name}.");
        }
    }

    /// <summary>
    /// Decodes the payload of one image Data URL.
    /// </summary>
    /// <param name="dataUrl">The Data URL.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] DecodeDataUrl(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0)
            throw new VisualBriefingBuildException(VisualBriefingFailureCode.SOURCE_PREPARATION_FAILED, VisualBriefingBuildStage.SOURCE_PREPARATION, "A visual asset could not be prepared.", "The image optimizer returned an invalid Data URL.");
        
        return Convert.FromBase64String(dataUrl[(comma + 1)..]);
    }

    /// <summary>
    /// Deletes a temporary source-preparation directory on a best-effort basis.
    /// </summary>
    /// <param name="temporaryDirectory">The temporary directory.</param>
    private static void DeleteTemporaryDirectory(string temporaryDirectory)
    {
        try
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
        catch
        {
            // Temporary optimized visual assets are cleaned up best effort.
        }
    }

    /// <summary>
    /// Creates a logging event from a stable identifier.
    /// </summary>
    /// <param name="eventId">The stable event identifier.</param>
    /// <returns>The logging event.</returns>
    private static EventId Event(VisualBriefingLogEventId eventId) => new((int)eventId, eventId.ToString());
}