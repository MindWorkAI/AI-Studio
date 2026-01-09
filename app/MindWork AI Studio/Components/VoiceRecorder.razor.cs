using AIStudio.Provider;
using AIStudio.Tools.MIME;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class VoiceRecorder : MSGComponentBase
{
    [Inject]
    private ILogger<VoiceRecorder> Logger { get; init; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    private uint numReceivedChunks;
    private bool isRecording;
    private bool isTranscribing;
    private FileStream? currentRecordingStream;
    private string? currentRecordingPath;
    private string? currentRecordingMimeType;
    private string? finalRecordingPath;
    private DotNetObjectReference<VoiceRecorder>? dotNetReference;

    private string Tooltip => this.isTranscribing
        ? T("Transcription in progress...")
        : this.isRecording
            ? T("Stop recording and start transcription")
            : T("Start recording your voice for a transcription");
    
    private async Task OnRecordingToggled(bool toggled)
    {
        if (toggled)
        {
            var mimeTypes = GetPreferredMimeTypes(
                Builder.Create().UseAudio().UseSubtype(AudioSubtype.OGG).Build(),
                Builder.Create().UseAudio().UseSubtype(AudioSubtype.AAC).Build(),
                Builder.Create().UseAudio().UseSubtype(AudioSubtype.MP3).Build(),
                Builder.Create().UseAudio().UseSubtype(AudioSubtype.AIFF).Build(),
                Builder.Create().UseAudio().UseSubtype(AudioSubtype.WAV).Build(),
                Builder.Create().UseAudio().UseSubtype(AudioSubtype.FLAC).Build()
            );

            this.Logger.LogInformation("Starting audio recording with preferred MIME types: '{PreferredMimeTypes}'.", string.Join<MIMEType>(", ", mimeTypes));

            // Create a DotNetObjectReference to pass to JavaScript:
            this.dotNetReference = DotNetObjectReference.Create(this);

            // Initialize the file stream for writing chunks:
            await this.InitializeRecordingStream();

            var mimeTypeStrings = mimeTypes.ToStringArray();
            var actualMimeType = await this.JsRuntime.InvokeAsync<string>("audioRecorder.start", this.dotNetReference, mimeTypeStrings);

            // Store the MIME type for later use:
            this.currentRecordingMimeType = actualMimeType;

            this.Logger.LogInformation("Audio recording started with MIME type: '{ActualMimeType}'.", actualMimeType);
            this.isRecording = true;
        }
        else
        {
            var result = await this.JsRuntime.InvokeAsync<AudioRecordingResult>("audioRecorder.stop");
            if (result.ChangedMimeType)
                this.Logger.LogWarning("The recorded audio MIME type was changed to '{ResultMimeType}'.", result.MimeType);

            // Close and finalize the recording stream:
            await this.FinalizeRecordingStream();

            this.isRecording = false;
            this.StateHasChanged();

            // Start transcription if we have a recording and a configured provider:
            if (this.finalRecordingPath is not null)
                await this.TranscribeRecordingAsync();
        }
    }

    private static MIMEType[] GetPreferredMimeTypes(params MIMEType[] mimeTypes)
    {
        // Default list if no parameters provided:
        if (mimeTypes.Length is 0)
        {
            var audioBuilder = Builder.Create().UseAudio();
            return
            [
                audioBuilder.UseSubtype(AudioSubtype.WEBM).Build(),
                audioBuilder.UseSubtype(AudioSubtype.OGG).Build(),
                audioBuilder.UseSubtype(AudioSubtype.MP4).Build(),
                audioBuilder.UseSubtype(AudioSubtype.MPEG).Build(),
            ];
        }

        return mimeTypes;
    }

    private async Task InitializeRecordingStream()
    {
        this.numReceivedChunks = 0;
        var dataDirectory = await this.RustService.GetDataDirectory();
        var recordingDirectory = Path.Combine(dataDirectory, "audioRecordings");
        if (!Directory.Exists(recordingDirectory))
            Directory.CreateDirectory(recordingDirectory);

        var fileName = $"recording_{DateTime.UtcNow:yyyyMMdd_HHmmss}.audio";
        this.currentRecordingPath = Path.Combine(recordingDirectory, fileName);
        this.currentRecordingStream = new FileStream(this.currentRecordingPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true);

        this.Logger.LogInformation("Initialized audio recording stream: '{RecordingPath}'.", this.currentRecordingPath);
    }

    [JSInvokable]
    public async Task OnAudioChunkReceived(byte[] chunkBytes)
    {
        if (this.currentRecordingStream is null)
        {
            this.Logger.LogWarning("Received audio chunk but no recording stream is active.");
            return;
        }

        try
        {
            this.numReceivedChunks++;
            await this.currentRecordingStream.WriteAsync(chunkBytes);
            await this.currentRecordingStream.FlushAsync();

            this.Logger.LogDebug("Wrote {ByteCount} bytes to recording stream.", chunkBytes.Length);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error writing audio chunk to stream.");
        }
    }

    private async Task FinalizeRecordingStream()
    {
        this.finalRecordingPath = null;
        if (this.currentRecordingStream is not null)
        {
            await this.currentRecordingStream.FlushAsync();
            await this.currentRecordingStream.DisposeAsync();
            this.currentRecordingStream = null;

            // Rename the file with the correct extension based on MIME type:
            if (this.currentRecordingPath is not null && this.currentRecordingMimeType is not null)
            {
                var extension = GetFileExtension(this.currentRecordingMimeType);
                var newPath = Path.ChangeExtension(this.currentRecordingPath, extension);

                if (File.Exists(this.currentRecordingPath))
                {
                    File.Move(this.currentRecordingPath, newPath, overwrite: true);
                    this.finalRecordingPath = newPath;
                    this.Logger.LogInformation("Finalized audio recording over {NumChunks} streamed audio chunks to the file '{RecordingPath}'.", this.numReceivedChunks, newPath);
                }
            }
        }

        this.currentRecordingPath = null;
        this.currentRecordingMimeType = null;

        // Dispose the .NET reference:
        this.dotNetReference?.Dispose();
        this.dotNetReference = null;
    }

    private static string GetFileExtension(string mimeType)
    {
        var baseMimeType = mimeType.Split(';')[0].Trim().ToLowerInvariant();
        return baseMimeType switch
        {
            "audio/webm" => ".webm",
            "audio/ogg" => ".ogg",
            "audio/mp4" => ".m4a",
            "audio/mpeg" => ".mp3",
            "audio/wav" => ".wav",
            "audio/x-wav" => ".wav",
            _ => ".audio" // Fallback
        };
    }

    private async Task TranscribeRecordingAsync()
    {
        if (this.finalRecordingPath is null)
            return;

        this.isTranscribing = true;
        this.StateHasChanged();

        try
        {
            // Get the configured transcription provider ID:
            var transcriptionProviderId = this.SettingsManager.ConfigurationData.App.UseTranscriptionProvider;
            if (string.IsNullOrWhiteSpace(transcriptionProviderId))
            {
                this.Logger.LogWarning("No transcription provider is configured.");
                await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, this.T("No transcription provider is configured.")));
                return;
            }

            // Find the transcription provider in the list of configured providers:
            var transcriptionProviderSettings = this.SettingsManager.ConfigurationData.TranscriptionProviders
                .FirstOrDefault(x => x.Id == transcriptionProviderId);

            if (transcriptionProviderSettings is null)
            {
                this.Logger.LogWarning("The configured transcription provider with ID '{ProviderId}' was not found.", transcriptionProviderId);
                await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, this.T("The configured transcription provider was not found.")));
                return;
            }

            // Check the confidence level:
            var minimumLevel = this.SettingsManager.GetMinimumConfidenceLevel(Tools.Components.NONE);
            var providerConfidence = transcriptionProviderSettings.UsedLLMProvider.GetConfidence(this.SettingsManager);
            if (providerConfidence.Level < minimumLevel)
            {
                this.Logger.LogWarning(
                    "The configured transcription provider '{ProviderName}' has a confidence level of '{ProviderLevel}', which is below the minimum required level of '{MinimumLevel}'.",
                    transcriptionProviderSettings.Name,
                    providerConfidence.Level,
                    minimumLevel);
                await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, this.T("The configured transcription provider does not meet the minimum confidence level.")));
                return;
            }

            // Create the provider instance:
            var provider = transcriptionProviderSettings.CreateProvider();
            if (provider.Provider is LLMProviders.NONE)
            {
                this.Logger.LogError("Failed to create the transcription provider instance.");
                await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, this.T("Failed to create the transcription provider.")));
                return;
            }

            // Call the transcription API:
            this.Logger.LogInformation("Starting transcription with provider '{ProviderName}' and model '{ModelName}'.", transcriptionProviderSettings.Name, transcriptionProviderSettings.Model.DisplayName);
            var transcribedText = await provider.TranscribeAudioAsync(transcriptionProviderSettings.Model, this.finalRecordingPath, this.SettingsManager);

            if (string.IsNullOrWhiteSpace(transcribedText))
            {
                this.Logger.LogWarning("The transcription result is empty.");
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, this.T("The transcription result is empty.")));
                return;
            }

            this.Logger.LogInformation("Transcription completed successfully. Result length: {Length} characters.", transcribedText.Length);

            // Play the transcription done sound effect:
            await this.JsRuntime.InvokeVoidAsync("playSound", "/sounds/transcription_done.ogg");

            // Copy the transcribed text to the clipboard:
            await this.RustService.CopyText2Clipboard(this.Snackbar, transcribedText);

            // Delete the recording file:
            try
            {
                if (File.Exists(this.finalRecordingPath))
                {
                    File.Delete(this.finalRecordingPath);
                    this.Logger.LogInformation("Deleted the recording file '{RecordingPath}'.", this.finalRecordingPath);
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to delete the recording file '{RecordingPath}'.", this.finalRecordingPath);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "An error occurred during transcription.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, this.T("An error occurred during transcription.")));
        }
        finally
        {
            this.finalRecordingPath = null;
            this.isTranscribing = false;
            this.StateHasChanged();
        }
    }

    private sealed class AudioRecordingResult
    {
        public string MimeType { get; init; } = string.Empty;

        public bool ChangedMimeType { get; init; }
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        // Clean up recording resources if still active:
        if (this.currentRecordingStream is not null)
        {
            this.currentRecordingStream.Dispose();
            this.currentRecordingStream = null;
        }

        this.dotNetReference?.Dispose();
        this.dotNetReference = null;
        base.DisposeResources();
    }

    #endregion
}
