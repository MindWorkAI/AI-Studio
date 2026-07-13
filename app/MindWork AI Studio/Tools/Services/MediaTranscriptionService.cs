using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed class MediaTranscriptionService(RustService rustService, SettingsManager settingsManager, ILogger<MediaTranscriptionService> logger) : IDisposable
{
    private readonly SemaphoreSlim queue = new(1, 1);
    private readonly Lock stateLock = new();
    private CancellationTokenSource? currentCancellation;
    private string? currentJobId;

    public event Action? StateChanged;

    public bool IsBusy { get; private set; }

    public string CurrentFileName { get; private set; } = string.Empty;

    public MediaTranscriptionPhase Phase { get; private set; } = MediaTranscriptionPhase.IDLE;

    public double? Progress { get; private set; }

    public async Task<TranscriptionResult> TranscribeAsync(string mediaPath, CancellationToken token = default)
    {
        await this.queue.WaitAsync(token);
        var normalizedPath = Path.Combine(
            Path.GetTempPath(),
            "mindwork-ai-studio-media",
            $"{Guid.NewGuid():N}.webm");
        
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedPath)!);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        lock (this.stateLock)
            this.currentCancellation = operationCancellation;

        try
        {
            this.UpdateState(true, Path.GetFileName(mediaPath), MediaTranscriptionPhase.PROBING, 0.0);
            var jobId = await rustService.StartMediaJobAsync(mediaPath, normalizedPath, operationCancellation.Token);
            lock (this.stateLock)
                this.currentJobId = jobId;

            MediaJobResult? normalized = null;
            await foreach (var mediaEvent in rustService.StreamMediaJobEventsAsync(jobId, operationCancellation.Token))
            {
                switch (mediaEvent.Phase)
                {
                    case MediaJobPhase.PROBING:
                        this.UpdateState(true, this.CurrentFileName, MediaTranscriptionPhase.PROBING, mediaEvent.Progress);
                        break;
                    
                    case MediaJobPhase.TRANSCODING:
                        this.UpdateState(true, this.CurrentFileName, MediaTranscriptionPhase.TRANSCODING, mediaEvent.Progress);
                        break;
                    
                    case MediaJobPhase.COMPLETED:
                        normalized = mediaEvent.Result;
                        break;
                    
                    case MediaJobPhase.CANCELLED:
                        throw new OperationCanceledException(operationCancellation.Token);
                    
                    case MediaJobPhase.FAILED:
                        return TranscriptionResult.Failure(mediaEvent.Error?.Message ?? "The media file could not be prepared.");
                }
            }

            if (normalized is null)
                return TranscriptionResult.Failure("The media pipeline ended without an output file.");

            var providerSettings = this.ResolveProvider();
            if (providerSettings is null)
                return TranscriptionResult.Failure("No usable transcription provider is configured.");

            this.UpdateState(true, this.CurrentFileName, MediaTranscriptionPhase.UPLOADING, null);
            
            var provider = providerSettings.CreateProvider();
            if (provider.Provider is LLMProviders.NONE)
                return TranscriptionResult.Failure("The configured transcription provider could not be created.");

            logger.LogInformation(
                "Transcribing normalized media '{MediaPath}' with provider '{Provider}' and model '{Model}'.",
                mediaPath,
                providerSettings.UsedLLMProvider,
                providerSettings.Model);
            
            var result = await provider.TranscribeAudioAsync(
                providerSettings.Model,
                normalized.OutputPath,
                settingsManager,
                operationCancellation.Token);
            
            return result.Success
                ? TranscriptionResult.FromText(result.Text.Trim())
                : result;
        }
        catch (OperationCanceledException)
        {
            return TranscriptionResult.Failure("The media transcription was cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Media transcription failed for '{MediaPath}'.", mediaPath);
            return TranscriptionResult.Failure("The media file could not be transcribed.");
        }
        finally
        {
            lock (this.stateLock)
            {
                this.currentJobId = null;
                this.currentCancellation = null;
            }
            try
            {
                if (File.Exists(normalizedPath))
                    File.Delete(normalizedPath);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not delete normalized media file '{NormalizedPath}'.", normalizedPath);
            }
            
            this.UpdateState(false, string.Empty, MediaTranscriptionPhase.IDLE, null);
            this.queue.Release();
        }
    }

    public async Task StopAsync()
    {
        string? jobId;
        lock (this.stateLock)
        {
            this.currentCancellation?.Cancel();
            jobId = this.currentJobId;
        }
        
        if (!string.IsNullOrWhiteSpace(jobId))
            await rustService.CancelMediaJobAsync(jobId);
    }

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

    private void UpdateState(bool isBusy, string fileName, MediaTranscriptionPhase phase, double? progress)
    {
        this.IsBusy = isBusy;
        this.CurrentFileName = fileName;
        this.Phase = phase;
        this.Progress = progress;
        this.StateChanged?.Invoke();
    }

    public void Dispose()
    {
        this.currentCancellation?.Cancel();
        this.currentCancellation?.Dispose();
        this.queue.Dispose();
    }
}