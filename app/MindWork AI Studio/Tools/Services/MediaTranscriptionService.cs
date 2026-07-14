using AIStudio.Chat;
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

    /// <summary>Protects operation ownership and owner-specific import state.</summary>
    private readonly Lock stateLock = new();

    /// <summary>All operations retained so disposal can cancel voice and import work.</summary>
    private readonly HashSet<MediaOperation> operations = [];

    /// <summary>The active or queued import operation for each owner.</summary>
    private readonly Dictionary<MediaImportOwner, MediaOperation> currentImports = [];

    /// <summary>The latest copied state retained for each owner across navigation.</summary>
    private readonly Dictionary<MediaImportOwner, MediaImportSnapshot> snapshots = [];

    /// <summary>Owners whose complete file batches are managed by this service.</summary>
    private readonly HashSet<MediaImportOwner> activeBatches = [];

    /// <summary>Batch-level cancellation keeps Stop effective between two files.</summary>
    private readonly Dictionary<MediaImportOwner, CancellationTokenSource> batchCancellations = [];

    /// <summary>Prevents new work after disposal.</summary>
    private bool disposed;

    /// <summary>Raised only with the owner whose copied state changed.</summary>
    public event Action<MediaImportOwner>? StateChanged;

    /// <summary>Gets whether one owner has queued, running, or canceling media work.</summary>
    public bool IsBusy(MediaImportOwner owner) => this.GetSnapshot(owner)?.IsBusy ?? false;

    /// <summary>Gets the last retained state for one owner.</summary>
    public MediaImportSnapshot? GetSnapshot(MediaImportOwner owner)
    {
        lock (this.stateLock)
            return this.snapshots.GetValueOrDefault(owner);
    }

    /// <summary>Gets copied retained snapshots for navigation indicators.</summary>
    public IReadOnlyCollection<MediaImportSnapshot> GetSnapshots()
    {
        lock (this.stateLock)
            return [.. this.snapshots.Values];
    }

    /// <summary>Starts an owner-managed attachment batch and returns without holding the UI event handler.</summary>
    public bool TryStartAttachmentBatch(IReadOnlyList<string> mediaPaths, MediaImportOwner owner, ChatThread? ownerChat = null)
    {
        this.ThrowIfDisposed();
        lock (this.stateLock)
        {
            if (!this.activeBatches.Add(owner))
                return false;

            this.batchCancellations[owner] = new();
        }

        _ = this.RunAttachmentBatchAsync(mediaPaths, owner, ownerChat);
        return true;
    }

    /// <summary>Starts a reattachable file-content import for one stable assistant field.</summary>
    public bool TryStartTextImport(string mediaPath, MediaImportTarget target)
    {
        this.ThrowIfDisposed();
        lock (this.stateLock)
        {
            if (!this.activeBatches.Add(target.Owner))
                return false;

            this.batchCancellations[target.Owner] = new();
        }

        _ = this.RunTextImportAsync(mediaPath, target);
        return true;
    }

    /// <summary>Completes a field import independently of the originating Blazor component.</summary>
    private async Task RunTextImportAsync(string mediaPath, MediaImportTarget target)
    {
        CancellationTokenSource cancellation;
        lock (this.stateLock)
            cancellation = this.batchCancellations[target.Owner];

        try
        {
            var result = await this.TranscribeImportAsync(mediaPath, target.Owner, cancellation.Token);
            if (result.Status is MediaTranscriptionResultStatus.SUCCEEDED)
                this.AddCompletedText(target, result.Text);
        }
        catch (OperationCanceledException)
        {
            var current = this.GetSnapshot(target.Owner);
            this.UpdateImportState(target.Owner, current?.CurrentFileName ?? string.Empty, MediaTranscriptionPhase.IDLE, null, MediaImportStatus.CANCELLED);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Owner media text import failed for '{Owner}' and target '{TargetId}'.", target.Owner, target.TargetId);
        }
        finally
        {
            lock (this.stateLock)
            {
                this.activeBatches.Remove(target.Owner);
                if (this.batchCancellations.Remove(target.Owner, out var ownedCancellation))
                    ownedCancellation.Dispose();
            }
        }
    }

    /// <summary>Stores a completed field transcript for reattachment after navigation.</summary>
    private void AddCompletedText(MediaImportTarget target, string text)
    {
        lock (this.stateLock)
        {
            var current = this.snapshots.GetValueOrDefault(target.Owner);
            if (current is null)
                return;

            var completed = new Dictionary<string, string>(current.CompletedTextTargets, StringComparer.Ordinal)
            {
                [target.TargetId] = text,
            };
            this.snapshots[target.Owner] = current with { CompletedTextTargets = completed };
        }

        this.StateChanged?.Invoke(target.Owner);
    }

    /// <summary>Serially transcribes a complete owner batch while retaining every successful result.</summary>
    private async Task RunAttachmentBatchAsync(IReadOnlyList<string> mediaPaths, MediaImportOwner owner, ChatThread? ownerChat)
    {
        CancellationToken batchToken;
        lock (this.stateLock)
            batchToken = this.batchCancellations[owner].Token;

        try
        {
            foreach (var mediaPath in mediaPaths)
            {
                batchToken.ThrowIfCancellationRequested();
                var result = await this.TranscribeImportAsync(mediaPath, owner, batchToken);
                if (result.Status is MediaTranscriptionResultStatus.CANCELLED)
                    break;

                if (result.Status is not MediaTranscriptionResultStatus.SUCCEEDED)
                    continue;

                var isPersistedChat = ownerChat is not null
                                      && WorkspaceBehaviour.IsChatExisting(new LoadChat(ownerChat.WorkspaceId, ownerChat.ChatId));
                var attachment = isPersistedChat
                    ? await WorkspaceBehaviour.CreateManagedTranscriptAsync(ownerChat!, mediaPath, result.Text)
                    : await ManagedTranscriptAttachment.CreateStagedAsync(mediaPath, result.Text);

                if (ownerChat is not null && attachment is { } managed
                    && ownerChat.PendingMediaTranscripts.All(existing => existing.FilePath != managed.FilePath))
                    ownerChat.PendingMediaTranscripts.Add(managed);

                if (isPersistedChat)
                    await WorkspaceBehaviour.StoreChatAsync(ownerChat!);

                this.AddCompletedAttachment(owner, attachment);
            }
        }
        catch (OperationCanceledException)
        {
            var current = this.GetSnapshot(owner);
            this.UpdateImportState(owner, current?.CurrentFileName ?? string.Empty, MediaTranscriptionPhase.IDLE, null, MediaImportStatus.CANCELLED);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Owner media batch failed for '{Owner}'.", owner);
        }
        finally
        {
            lock (this.stateLock)
            {
                this.activeBatches.Remove(owner);
                if (this.batchCancellations.Remove(owner, out var cancellation))
                    cancellation.Dispose();
            }
        }
    }

    /// <summary>Adds a successful partial result to the retained owner snapshot.</summary>
    private void AddCompletedAttachment(MediaImportOwner owner, FileAttachment attachment)
    {
        lock (this.stateLock)
        {
            var current = this.snapshots.GetValueOrDefault(owner);
            if (current is null)
                return;

            this.snapshots[owner] = current with { CompletedAttachments = [..current.CompletedAttachments, attachment] };
        }

        this.StateChanged?.Invoke(owner);
    }

    /// <summary>
    /// Transcribes an attachment or file-content import on the serialized visible lane.
    /// </summary>
    /// <param name="mediaPath">Source media path.</param>
    /// <param name="owner">Media import owner.</param>
    /// <param name="token">Caller cancellation token.</param>
    /// <returns>A typed terminal result.</returns>
    private async Task<MediaTranscriptionResult> TranscribeImportAsync(string mediaPath, MediaImportOwner owner, CancellationToken token = default)
    {
        this.ThrowIfDisposed();
        var operation = this.CreateOperation(owner, token);
        lock (this.stateLock)
        {
            if (!this.currentImports.TryAdd(owner, operation))
                throw new InvalidOperationException($"Media owner '{owner}' already has an active operation.");
        }
        this.UpdateImportState(owner, Path.GetFileName(mediaPath), MediaTranscriptionPhase.QUEUED, null, MediaImportStatus.QUEUED);

        try
        {
            await this.importQueue.WaitAsync(operation.Cancellation.Token);
            operation.HasQueueLease = true;
            this.UpdateImportState(owner, Path.GetFileName(mediaPath), MediaTranscriptionPhase.PROBING, 0.0, MediaImportStatus.RUNNING);
            var result = await this.TranscribeCoreAsync(mediaPath, operation, updateImportState: true);
            
            var status = result.Status switch
            {
                MediaTranscriptionResultStatus.SUCCEEDED => MediaImportStatus.SUCCEEDED,
                MediaTranscriptionResultStatus.CANCELLED => MediaImportStatus.CANCELLED,
                _ => MediaImportStatus.FAILED,
            };
            
            this.UpdateImportState(owner, Path.GetFileName(mediaPath), MediaTranscriptionPhase.IDLE, null, status);
            return result;
        }
        catch (OperationCanceledException)
        {
            this.UpdateImportState(owner, Path.GetFileName(mediaPath), MediaTranscriptionPhase.IDLE, null, MediaImportStatus.CANCELLED);
            return MediaTranscriptionResult.Cancelled();
        }
        finally
        {
            lock (this.stateLock)
            {
                if (this.currentImports.GetValueOrDefault(owner) == operation)
                    this.currentImports.Remove(owner);
            }

            this.ReleaseOperation(operation);
            if (operation.HasQueueLease)
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
        var operation = this.CreateOperation(null, token);
        
        try
        {
            return await this.TranscribeCoreAsync(mediaPath, operation, updateImportState: false);
        }
        finally
        {
            this.ReleaseOperation(operation);
        }
    }

    /// <summary>Cancels only the queued or active operation belonging to one owner.</summary>
    public async Task StopAsync(MediaImportOwner owner)
    {
        MediaOperation? operation;
        lock (this.stateLock)
        {
            operation = this.currentImports.GetValueOrDefault(owner);
            this.batchCancellations.GetValueOrDefault(owner)?.Cancel();
            operation?.Cancellation.Cancel();
        }

        if (operation is not null)
            this.UpdateImportState(owner, this.GetSnapshot(owner)?.CurrentFileName ?? string.Empty, MediaTranscriptionPhase.CANCELING, null, MediaImportStatus.CANCELING);

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
                this.UpdateImportState(operation.Owner!.Value, Path.GetFileName(mediaPath), MediaTranscriptionPhase.UPLOADING, null, MediaImportStatus.RUNNING);

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
            if (!this.RetainNormalizedMediaIfRequested(normalizedPath, operation.Id))
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
                    this.UpdateImportState(operation.Owner!.Value, Path.GetFileName(mediaPath), phase, mediaEvent.Progress, MediaImportStatus.RUNNING);
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
    /// <param name="owner">Media import owner.</param>
    /// <param name="token">Caller token linked to the operation.</param>
    /// <returns>The registered operation.</returns>
    private MediaOperation CreateOperation(MediaImportOwner? owner, CancellationToken token)
    {
        var operation = new MediaOperation(owner, token);
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

    /// <summary>Updates and publishes copied state for exactly one owner.</summary>
    private void UpdateImportState(MediaImportOwner owner, string fileName, MediaTranscriptionPhase phase, double? progress, MediaImportStatus status)
    {
        var snapshot = new MediaImportSnapshot
        {
            Owner = owner,
            CurrentFileName = fileName,
            Phase = phase,
            Progress = progress,
            Status = status,
            CompletedAttachments = this.GetSnapshot(owner)?.CompletedAttachments ?? [],
            CompletedTextTargets = this.GetSnapshot(owner)?.CompletedTextTargets ?? new Dictionary<string, string>(),
        };
        
        lock (this.stateLock)
            this.snapshots[owner] = snapshot;

        this.StateChanged?.Invoke(owner);
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

    /// <summary>Retains the exact provider upload only for opt-in debug diagnostics.</summary>
    private bool RetainNormalizedMediaIfRequested(string normalizedPath, Guid operationId)
    {
#if DEBUG
        if (!string.Equals(Environment.GetEnvironmentVariable("MINDWORK_AI_RETAIN_NORMALIZED_MEDIA"), "true", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(normalizedPath))
            return false;

        try
        {
            var diagnosticDirectory = Path.Combine(Path.GetTempPath(), "mindwork-ai-studio-media", "diagnostics");
            Directory.CreateDirectory(diagnosticDirectory);
            var diagnosticPath = Path.Combine(diagnosticDirectory, $"{operationId:N}.webm");
            File.Move(normalizedPath, diagnosticPath, overwrite: true);
            
            foreach (var oldPath in new DirectoryInfo(diagnosticDirectory).EnumerateFiles("*.webm").OrderByDescending(file => file.LastWriteTimeUtc).Skip(10))
                oldPath.Delete();

            logger.LogInformation("Retained normalized media diagnostic '{DiagnosticPath}'.", diagnosticPath);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not retain normalized media diagnostic for operation '{OperationId}'.", operationId);
        }
#endif
        return false;
    }

    /// <summary>Returns localized text while registering the US-English fallback with I18N.</summary>
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MediaTranscriptionService).Namespace, nameof(MediaTranscriptionService));

    /// <summary>Throws when a caller attempts to start work after disposal.</summary>
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(this.disposed, this);

    /// <summary>Cancels every active import and voice operation and releases owned resources.</summary>
    public void Dispose()
    {
        MediaOperation[] active;
        CancellationTokenSource[] batches;
        lock (this.stateLock)
        {
            if (this.disposed)
                return;
            
            this.disposed = true;
            active = [.. this.operations];
            batches = [.. this.batchCancellations.Values];
        }
        foreach (var operation in active)
            operation.Cancellation.Cancel();
        
        foreach (var batch in batches)
            batch.Cancel();
        
        // The semaphore may still be released by an operation unwinding after cancellation.
    }

    /// <summary>Cancellation and runtime-job ownership for exactly one media operation.</summary>
    private sealed class MediaOperation : IDisposable
    {
        /// <summary>Creates operation state linked to a caller token.</summary>
        /// <param name="owner">Media import owner.</param>
        /// <param name="token">Caller cancellation token.</param>
        public MediaOperation(MediaImportOwner? owner, CancellationToken token)
        {
            this.Owner = owner;
            this.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        }

        /// <summary>Gets the optional visible import owner; voice operations have none.</summary>
        public MediaImportOwner? Owner { get; }

        /// <summary>Gets the unique temporary-path identifier.</summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>Gets the operation-owned cancellation source.</summary>
        public CancellationTokenSource Cancellation { get; }

        /// <summary>Gets or sets the Rust job after its POST response establishes ownership.</summary>
        public string? JobId { get; set; }

        /// <summary>Gets or sets whether this operation currently owns the serialized lane.</summary>
        public bool HasQueueLease { get; set; }

        /// <summary>Disposes operation-owned cancellation state.</summary>
        public void Dispose() => this.Cancellation.Dispose();
    }
}