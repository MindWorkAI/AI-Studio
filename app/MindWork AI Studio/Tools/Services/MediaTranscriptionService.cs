using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

/// <summary>
/// Coordinates serialized visible media imports and independent voice transcriptions.
/// </summary>
public sealed class MediaTranscriptionService(RustService rustService, SettingsManager settingsManager, ILogger<MediaTranscriptionService> logger) : IDisposable
{
    /// <summary>Serializes attachment and file-content imports.</summary>
    private readonly SemaphoreSlim importQueue = new(1, 1);

    /// <summary>Protects operation ownership and visible import state.</summary>
    private readonly Lock stateLock = new();

    /// <summary>All operations retained so disposal can cancel voice and import work.</summary>
    private readonly HashSet<MediaOperation> operations = [];

    /// <summary>The currently visible import operation, if any.</summary>
    private MediaOperation? currentImport;

    /// <summary>Prevents new work after disposal.</summary>
    private bool disposed;

    /// <summary>Raised whenever visible import state changes.</summary>
    public event Action? StateChanged;

    /// <summary>Gets whether the serialized import lane is active.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>Gets the file name shown for the active import.</summary>
    public string CurrentFileName { get; private set; } = string.Empty;

    /// <summary>Gets the active import phase.</summary>
    public MediaTranscriptionPhase Phase { get; private set; } = MediaTranscriptionPhase.IDLE;

    /// <summary>Gets optional import progress between zero and one.</summary>
    public double? Progress { get; private set; }

    /// <summary>
    /// Transcribes an attachment or file-content import on the serialized visible lane.
    /// </summary>
    /// <param name="mediaPath">Source media path.</param>
    /// <param name="token">Caller cancellation token.</param>
    /// <returns>A typed terminal result.</returns>
    public async Task<MediaTranscriptionResult> TranscribeImportAsync(string mediaPath, CancellationToken token = default)
    {
        this.ThrowIfDisposed();
        await this.importQueue.WaitAsync(token);
        var operation = this.CreateOperation(token);
        lock (this.stateLock)
            this.currentImport = operation;

        try
        {
            this.UpdateImportState(true, Path.GetFileName(mediaPath), MediaTranscriptionPhase.PROBING, 0.0);
            return await this.TranscribeCoreAsync(mediaPath, operation, updateImportState: true);
        }
        finally
        {
            lock (this.stateLock)
            {
                if (ReferenceEquals(this.currentImport, operation))
                    this.currentImport = null;
            }

            this.ReleaseOperation(operation);
            this.UpdateImportState(false, string.Empty, MediaTranscriptionPhase.IDLE, null);
            this.importQueue.Release();
        }
    }

    /// <summary>
    /// Transcribes a voice recording independently of the visible import lane.
    /// </summary>
    /// <param name="mediaPath">Voice recording path.</param>
    /// <param name="token">Caller cancellation token.</param>
    /// <returns>A typed terminal result.</returns>
    public async Task<MediaTranscriptionResult> TranscribeVoiceAsync(string mediaPath, CancellationToken token = default)
    {
        this.ThrowIfDisposed();
        var operation = this.CreateOperation(token);
        
        try
        {
            return await this.TranscribeCoreAsync(mediaPath, operation, updateImportState: false);
        }
        finally
        {
            this.ReleaseOperation(operation);
        }
    }

    /// <summary>Cancels only the active visible import operation.</summary>
    public async Task StopAsync()
    {
        MediaOperation? operation;
        lock (this.stateLock)
        {
            operation = this.currentImport;
            operation?.Cancellation.Cancel();
        }

        if (!string.IsNullOrWhiteSpace(operation?.JobId))
            await rustService.CancelMediaJobAsync(operation.JobId);
    }

    /// <summary>Runs normalization, provider resolution, and upload for one owned operation.</summary>
    /// <param name="mediaPath">Source media path.</param>
    /// <param name="operation">Operation-specific cancellation and Rust-job state.</param>
    /// <param name="updateImportState">Whether progress belongs to the visible import lane.</param>
    /// <returns>A typed terminal result.</returns>
    private async Task<MediaTranscriptionResult> TranscribeCoreAsync(string mediaPath, MediaOperation operation, bool updateImportState)
    {
        var normalizedPath = Path.Combine(Path.GetTempPath(), "mindwork-ai-studio-media", $"{operation.Id:N}.webm");
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedPath)!);

        try
        {
            var normalized = await this.NormalizeAsync(mediaPath, normalizedPath, operation, updateImportState);
            if (normalized.Result is null)
                return normalized.Error is null
                    ? MediaTranscriptionResult.Failed(TB("The media pipeline ended without an output file."))
                    : MediaTranscriptionResult.Failed(UserMessageFor(normalized.Error.Code), normalized.Error.Code);

            var providerSettings = this.ResolveProvider();
            if (providerSettings is null)
                return MediaTranscriptionResult.Failed(TB("No usable transcription provider is configured."));

            if (updateImportState)
                this.UpdateImportState(true, this.CurrentFileName, MediaTranscriptionPhase.UPLOADING, null);

            var provider = providerSettings.CreateProvider();
            if (provider.Provider is LLMProviders.NONE)
                return MediaTranscriptionResult.Failed(TB("The configured transcription provider could not be created."));

            logger.LogInformation("Transcribing normalized media '{MediaPath}' with provider '{Provider}' and model '{Model}'.",
                mediaPath,
                providerSettings.UsedLLMProvider,
                providerSettings.Model);

            var providerResult = await provider.TranscribeAudioAsync(providerSettings.Model, normalized.Result.OutputPath, settingsManager, operation.Cancellation.Token);
            if (!providerResult.Success)
            {
                logger.LogWarning("The transcription provider failed for '{MediaPath}': {Diagnostic}", mediaPath, providerResult.ErrorMessage);
                return MediaTranscriptionResult.Failed(TB("The transcription provider could not transcribe the media file."));
            }

            return MediaTranscriptionResult.Succeeded(providerResult.Text.Trim());
        }
        catch (OperationCanceledException)
        {
            return MediaTranscriptionResult.Cancelled();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Media transcription failed for '{MediaPath}'.", mediaPath);
            return MediaTranscriptionResult.Failed(TB("The media file could not be transcribed."));
        }
        finally
        {
            // NormalizeAsync does not return from cancellation until Rust has reached a terminal
            // phase, so deleting both paths here cannot race a still-writing worker.
            this.DeleteTemporaryFile(normalizedPath);
            this.DeleteTemporaryFile(normalizedPath + ".partial");
        }
    }

    /// <summary>Runs the Rust normalization job and drains cancellation to a terminal event.</summary>
    /// <param name="mediaPath">Source media path.</param>
    /// <param name="normalizedPath">Owned temporary output path.</param>
    /// <param name="operation">Operation-specific state.</param>
    /// <param name="updateImportState">Whether progress belongs to the import lane.</param>
    /// <returns>The terminal runtime result or error.</returns>
    private async Task<(MediaJobResult? Result, MediaJobError? Error)> NormalizeAsync(
        string mediaPath,
        string normalizedPath,
        MediaOperation operation,
        bool updateImportState)
    {
        // The quick POST is intentionally not cancelled: losing its response could orphan a job
        // whose ID the client never received. Cancellation is applied immediately after ownership.
        var jobId = await rustService.StartMediaJobAsync(mediaPath, normalizedPath, CancellationToken.None);
        operation.JobId = jobId;

        try
        {
            operation.Cancellation.Token.ThrowIfCancellationRequested();
            await foreach (var mediaEvent in rustService.StreamMediaJobEventsAsync(jobId, operation.Cancellation.Token))
            {
                if (updateImportState && mediaEvent.Phase is MediaJobPhase.PROBING or MediaJobPhase.TRANSCODING)
                {
                    var phase = mediaEvent.Phase is MediaJobPhase.PROBING
                        ? MediaTranscriptionPhase.PROBING
                        : MediaTranscriptionPhase.TRANSCODING;
                    this.UpdateImportState(true, this.CurrentFileName, phase, mediaEvent.Progress);
                }

                switch (mediaEvent.Phase)
                {
                    case MediaJobPhase.COMPLETED:
                        return (mediaEvent.Result, null);
                    
                    case MediaJobPhase.FAILED:
                        if (mediaEvent.Error is not null)
                            logger.LogWarning("Rust media normalization failed for '{MediaPath}' with {Code}: {Diagnostic}", mediaPath, mediaEvent.Error.Code, mediaEvent.Error.Message);
                        
                        return (null, mediaEvent.Error);
                    
                    case MediaJobPhase.CANCELLED:
                        throw new OperationCanceledException(operation.Cancellation.Token);
                }
            }

            return (null, null);
        }
        catch (OperationCanceledException)
        {
            await rustService.CancelMediaJobAsync(jobId, CancellationToken.None);
            await this.DrainTerminalEventAsync(jobId);
            throw;
        }
    }

    /// <summary>Waits for Rust cleanup after cooperative cancellation.</summary>
    /// <param name="jobId">Owned Rust job identifier.</param>
    private async Task DrainTerminalEventAsync(string jobId)
    {
        await foreach (var _ in rustService.StreamMediaJobEventsAsync(jobId, CancellationToken.None))
        {
            // The stream itself ends immediately after the first terminal snapshot or event.
        }
    }

    /// <summary>Resolves the configured provider after confidence validation.</summary>
    private TranscriptionProvider? ResolveProvider()
    {
        var providerId = settingsManager.ConfigurationData.App.UseTranscriptionProvider;
        if (string.IsNullOrWhiteSpace(providerId))
            return null;

        var providerSettings = settingsManager.ConfigurationData.TranscriptionProviders.FirstOrDefault(x => x.Id == providerId);
        if (providerSettings is null)
            return null;

        var minimumLevel = settingsManager.GetMinimumConfidenceLevel(Components.NONE);
        return providerSettings.UsedLLMProvider.GetConfidence(settingsManager).Level >= minimumLevel
            ? providerSettings
            : null;
    }

    /// <summary>Creates and registers operation-owned cancellation state.</summary>
    /// <param name="token">Caller token linked to the operation.</param>
    /// <returns>The registered operation.</returns>
    private MediaOperation CreateOperation(CancellationToken token)
    {
        var operation = new MediaOperation(token);
        lock (this.stateLock)
            this.operations.Add(operation);
        
        return operation;
    }

    /// <summary>Unregisters and disposes completed operation state.</summary>
    /// <param name="operation">Completed operation.</param>
    private void ReleaseOperation(MediaOperation operation)
    {
        lock (this.stateLock)
            this.operations.Remove(operation);
        
        operation.Dispose();
    }

    /// <summary>Updates state exposed only by the serialized import lane.</summary>
    private void UpdateImportState(bool isBusy, string fileName, MediaTranscriptionPhase phase, double? progress)
    {
        this.IsBusy = isBusy;
        this.CurrentFileName = fileName;
        this.Phase = phase;
        this.Progress = progress;
        this.StateChanged?.Invoke();
    }

    /// <summary>Maps runtime codes to localized user-facing fallback text.</summary>
    private static string UserMessageFor(MediaJobErrorCode code) => code switch
    {
        MediaJobErrorCode.FILE_NOT_FOUND => TB("The selected media file no longer exists."),
        MediaJobErrorCode.UNSAFE_FILE or MediaJobErrorCode.NOT_MEDIA => TB("The selected file cannot be processed as media."),
        MediaJobErrorCode.NO_AUDIO_TRACK => TB("The selected media file does not contain an audio track."),
        MediaJobErrorCode.UNSUPPORTED_CONTAINER or MediaJobErrorCode.UNSUPPORTED_CODEC or MediaJobErrorCode.UNSUPPORTED_OPUS_MAPPING => TB("This media format or audio codec is not supported."),
        MediaJobErrorCode.UNKNOWN_FORMAT or MediaJobErrorCode.DAMAGED_CONTAINER => TB("The media file is damaged or its format could not be identified."),
        
        _ => TB("The media file could not be prepared for transcription."),
    };

    /// <summary>Deletes one operation-owned temporary file on a best-effort basis.</summary>
    private void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete operation-owned temporary media file '{Path}'.", path);
        }
    }

    /// <summary>Returns localized text while registering the US-English fallback with I18N.</summary>
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MediaTranscriptionService).Namespace, nameof(MediaTranscriptionService));

    /// <summary>Throws when a caller attempts to start work after disposal.</summary>
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(this.disposed, this);

    /// <summary>Cancels every active import and voice operation and releases owned resources.</summary>
    public void Dispose()
    {
        MediaOperation[] active;
        lock (this.stateLock)
        {
            if (this.disposed)
                return;
            
            this.disposed = true;
            active = [.. this.operations];
        }
        foreach (var operation in active)
            operation.Cancellation.Cancel();
        
        // The semaphore may still be released by an operation unwinding after cancellation.
    }

    /// <summary>Cancellation and runtime-job ownership for exactly one media operation.</summary>
    private sealed class MediaOperation : IDisposable
    {
        /// <summary>Creates operation state linked to a caller token.</summary>
        /// <param name="token">Caller cancellation token.</param>
        public MediaOperation(CancellationToken token)
        {
            this.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        }

        /// <summary>Gets the unique temporary-path identifier.</summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>Gets the operation-owned cancellation source.</summary>
        public CancellationTokenSource Cancellation { get; }

        /// <summary>Gets or sets the Rust job after its POST response establishes ownership.</summary>
        public string? JobId { get; set; }

        /// <summary>Disposes operation-owned cancellation state.</summary>
        public void Dispose() => this.Cancellation.Dispose();
    }
}