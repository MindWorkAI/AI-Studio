using System.Buffers.Binary;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class VoiceRecorder : MSGComponentBase
{
    private const int PCM_WAV_HEADER_SIZE = 44;

    [Inject]
    private ILogger<VoiceRecorder> Logger { get; init; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private GlobalShortcutService GlobalShortcutService { get; init; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    [Inject]
    private VoiceRecordingAvailabilityService VoiceRecordingAvailabilityService { get; init; } = null!;

    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.GlobalShortcutService.RuntimeStateChanged += this.OnShortcutRuntimeStateChanged;

        // Register for global shortcut events:
        this.ApplyFilters([], [Event.TAURI_EVENT_RECEIVED, Event.VOICE_RECORDING_AVAILABILITY_CHANGED]);

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            this.localShortcutDotNetReference = DotNetObjectReference.Create(this);
            this.localShortcutInteropReady = true;
            await this.ApplyLocalShortcutState(this.GlobalShortcutService.GetRuntimeState(Shortcut.VOICE_RECORDING_TOGGLE));

            if (this.ShouldRenderVoiceRecording)
                await this.EnsureSoundEffectsAvailableAsync("during the first interactive render");
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.GLOBAL_SHORTCUT_PRESSED } tauriEvent:
                // Check if this is the voice recording toggle shortcut:
                if (tauriEvent.TryGetShortcut(out var shortcutId) && shortcutId == Shortcut.VOICE_RECORDING_TOGGLE)
                {
                    this.Logger.LogInformation("Global shortcut triggered for voice recording toggle.");
                    await this.ToggleRecordingFromShortcut();
                }
                
                break;

            case Event.VOICE_RECORDING_AVAILABILITY_CHANGED:
                this.StateHasChanged();
                break;
        }
    }

    private async Task OnShortcutRuntimeStateChanged(GlobalShortcutRuntimeState runtimeState)
    {
        try
        {
            await this.InvokeAsync(() => this.ApplyLocalShortcutState(runtimeState));
        }
        catch (ObjectDisposedException)
        {
            this.Logger.LogDebug("Ignoring a shortcut state change after the voice recorder was disposed.");
        }
        catch (InvalidOperationException ex)
        {
            this.Logger.LogDebug(ex, "The focused-window shortcut listener could not be updated because the component dispatcher is unavailable.");
        }
    }

    [JSInvokable]
    public async Task OnLocalShortcutPressed()
    {
        var runtimeState = this.GlobalShortcutService.GetRuntimeState(Shortcut.VOICE_RECORDING_TOGGLE);
        if (runtimeState.Backend is not ShortcutBackend.LOCAL || runtimeState.IsSuspended)
        {
            this.Logger.LogDebug("Ignoring a stale focused-window shortcut event.");
            return;
        }

        this.Logger.LogInformation("Focused-window shortcut triggered for voice recording toggle.");
        await this.ToggleRecordingFromShortcut();
    }

    /// <summary>
    /// Toggles the recording state when triggered by a global shortcut.
    /// </summary>
    private async Task ToggleRecordingFromShortcut()
    {
        if (!this.IsVoiceRecordingAvailable)
        {
            this.Logger.LogDebug("Ignoring shortcut: voice recording is unavailable in the current session.");
            return;
        }

        // Don't allow toggle if transcription is in progress or preparing:
        if (this.isTranscribing || this.isPreparing)
        {
            this.Logger.LogDebug("Ignoring shortcut: transcription or preparation is in progress.");
            return;
        }

        // Toggle the recording state:
        await this.OnRecordingToggled(!this.isRecording);
    }

    #endregion

    private uint numReceivedChunks;
    private bool isRecording;
    private bool isPreparing;
    private bool isTranscribing;
    private FileStream? currentRecordingStream;
    private string? currentRecordingPath;
    private string? finalRecordingPath;
    private DotNetObjectReference<VoiceRecorder>? dotNetReference;
    private DotNetObjectReference<VoiceRecorder>? localShortcutDotNetReference;
    private bool localShortcutInteropReady;

    private async Task ApplyLocalShortcutState(GlobalShortcutRuntimeState runtimeState)
    {
        if (!this.localShortcutInteropReady
            || this.localShortcutDotNetReference is null
            || runtimeState.ShortcutId is not Shortcut.VOICE_RECORDING_TOGGLE)
        {
            return;
        }

        try
        {
            if (runtimeState.Backend is ShortcutBackend.LOCAL
                && !runtimeState.IsSuspended
                && !string.IsNullOrWhiteSpace(runtimeState.Shortcut))
            {
                await this.JsRuntime.InvokeVoidAsync(
                    "localShortcut.register",
                    "voice-recording-toggle",
                    runtimeState.Shortcut,
                    this.localShortcutDotNetReference);
            }
            else
            {
                await this.JsRuntime.InvokeVoidAsync("localShortcut.unregister", "voice-recording-toggle");
            }
        }
        catch (JSDisconnectedException)
        {
            this.Logger.LogDebug("The focused-window shortcut listener could not be updated because the JS runtime disconnected.");
        }
        catch (OperationCanceledException)
        {
            this.Logger.LogDebug("Updating the focused-window shortcut listener was canceled.");
        }
        catch (JSException ex)
        {
            this.Logger.LogWarning(ex, "Failed to update the focused-window shortcut listener.");
        }
    }

    private bool ShouldRenderVoiceRecording => PreviewFeatures.PRE_SPEECH_TO_TEXT_2026.IsEnabled(this.SettingsManager)
                                               && !string.IsNullOrWhiteSpace(this.SettingsManager.ConfigurationData.App.UseTranscriptionProvider);

    private bool IsVoiceRecordingAvailable => this.ShouldRenderVoiceRecording
                                              && this.VoiceRecordingAvailabilityService.IsAvailable;

    private string Tooltip => !this.VoiceRecordingAvailabilityService.IsAvailable
        ? T("Voice recording is unavailable because the client could not initialize audio playback.")
        : this.isTranscribing
            ? T("Transcription in progress...")
            : this.isRecording
                ? T("Stop recording and start transcription")
                : T("Start recording your voice for a transcription");
    
    private async Task OnRecordingToggled(bool toggled)
    {
        if (toggled)
        {
            if (!this.IsVoiceRecordingAvailable)
            {
                this.Logger.LogDebug("Ignoring recording start: voice recording is unavailable in the current session.");
                return;
            }

            this.isPreparing = true;
            this.StateHasChanged();

            if (!await this.EnsureSoundEffectsAvailableAsync("before starting audio recording"))
            {
                this.isPreparing = false;
                this.StateHasChanged();
                return;
            }

            this.Logger.LogInformation("Starting PCM/WAV audio recording.");

            // Create a DotNetObjectReference to pass to JavaScript:
            this.dotNetReference = DotNetObjectReference.Create(this);

            // Initialize the file stream for writing chunks:
            await this.InitializeRecordingStream();

            try
            {
                await this.JsRuntime.InvokeVoidAsync("audioRecorder.start", this.dotNetReference);
                this.Logger.LogInformation("PCM/WAV audio recording started.");
                this.isPreparing = false;
                this.isRecording = true;
            }
            catch (Exception e)
            {
                this.Logger.LogError(e, "Failed to start audio recording.");
                await this.MessageBus.SendError(new(Icons.Material.Filled.MicOff, this.T("Failed to start audio recording.")));

                // Clean up the recording stream if starting failed:
                await this.FinalizeRecordingStream();
                await this.ReleaseMicrophoneAsync();
            }
            finally
            {
                this.StateHasChanged();
            }
        }
        else
        {
            var recordingStoppedSuccessfully = false;
            try
            {
                await this.JsRuntime.InvokeVoidAsync("audioRecorder.stop");
                recordingStoppedSuccessfully = true;
            }
            catch (Exception e)
            {
                this.Logger.LogError(e, "Failed to stop audio recording.");
                await this.MessageBus.SendError(new(Icons.Material.Filled.MicOff, this.T("Failed to stop audio recording.")));
            }

            // Close and finalize the recording stream:
            await this.FinalizeRecordingStream();

            this.isRecording = false;
            this.StateHasChanged();

            if (!recordingStoppedSuccessfully || this.finalRecordingPath is null)
            {
                if (recordingStoppedSuccessfully)
                {
                    this.Logger.LogWarning("The audio recorder did not produce any data.");
                    await this.MessageBus.SendError(new(Icons.Material.Filled.MicOff, this.T("Failed to stop audio recording.")));
                }

                this.DeleteFinalRecording();
                await this.ReleaseMicrophoneAsync();
                return;
            }

            await this.TranscribeRecordingAsync();
        }
    }

    private async Task InitializeRecordingStream()
    {
        this.numReceivedChunks = 0;
        var dataDirectory = await this.RustService.GetDataDirectory();
        var recordingDirectory = Path.Combine(dataDirectory, "audioRecordings");
        if (!Directory.Exists(recordingDirectory))
            Directory.CreateDirectory(recordingDirectory);

        var fileName = $"recording_{DateTime.UtcNow:yyyyMMdd_HHmmss}.wav";
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
            throw;
        }
    }

    private async Task FinalizeRecordingStream()
    {
        this.finalRecordingPath = null;
        if (this.currentRecordingStream is not null)
        {
            await this.currentRecordingStream.FlushAsync();
            var hasPcmAudioData = await this.FinalizePcmWavHeaderAsync(this.currentRecordingStream);
            await this.currentRecordingStream.DisposeAsync();
            this.currentRecordingStream = null;

            if (this.currentRecordingPath is not null && File.Exists(this.currentRecordingPath))
            {
                var fileSize = new FileInfo(this.currentRecordingPath).Length;

                if (hasPcmAudioData)
                {
                    this.finalRecordingPath = this.currentRecordingPath;
                    this.Logger.LogInformation("Finalized audio recording over {NumChunks} streamed audio chunks to the file '{RecordingPath}' with {FileSize} bytes.", this.numReceivedChunks, this.currentRecordingPath, fileSize);
                }
                else
                {
                    this.Logger.LogWarning("Discarding a PCM/WAV audio recording without audio data ({FileSize} bytes).", fileSize);
                    File.Delete(this.currentRecordingPath);
                }
            }
        }

        this.currentRecordingPath = null;

        // Dispose the .NET reference:
        this.dotNetReference?.Dispose();
        this.dotNetReference = null;
    }

    private async Task<bool> FinalizePcmWavHeaderAsync(FileStream recordingStream)
    {
        if (recordingStream.Length <= PCM_WAV_HEADER_SIZE)
            return false;

        var pcmDataSize = recordingStream.Length - PCM_WAV_HEADER_SIZE;
        if (pcmDataSize > uint.MaxValue - 36)
            throw new InvalidDataException("The streamed PCM recording exceeds the WAV size limit.");

        var valueBuffer = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(valueBuffer, checked((uint)(36 + pcmDataSize)));
        recordingStream.Seek(4, SeekOrigin.Begin);
        await recordingStream.WriteAsync(valueBuffer);

        BinaryPrimitives.WriteUInt32LittleEndian(valueBuffer, checked((uint)pcmDataSize));
        recordingStream.Seek(40, SeekOrigin.Begin);
        await recordingStream.WriteAsync(valueBuffer);
        recordingStream.Seek(0, SeekOrigin.End);
        await recordingStream.FlushAsync();

        this.Logger.LogInformation("Finalized a streamed PCM/WAV header for {PcmDataSize} bytes of audio data.", pcmDataSize);
        return true;
    }

    private async Task TranscribeRecordingAsync()
    {
        if (this.finalRecordingPath is null)
        {
            // No recording to transcribe, but still release the microphone:
            await this.ReleaseMicrophoneAsync();
            return;
        }

        this.isTranscribing = true;
        this.StateHasChanged();

        try
        {
            var transcriptionResult = await this.MediaTranscriptionService.TranscribeVoiceAsync(this.finalRecordingPath);
            if (transcriptionResult.Status is not MediaTranscriptionResultStatus.SUCCEEDED)
            {
                if (transcriptionResult.Status is MediaTranscriptionResultStatus.CANCELLED)
                    return;

                if (transcriptionResult.Status is MediaTranscriptionResultStatus.NO_AUDIBLE_SIGNAL)
                {
                    await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, transcriptionResult.UserMessage));
                    return;
                }

                this.Logger.LogWarning("The transcription request failed.");
                var userMessage = string.IsNullOrWhiteSpace(transcriptionResult.UserMessage)
                    ? this.T("Unfortunately, there was an error communicating with the AI system.")
                    : transcriptionResult.UserMessage;
                await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, userMessage));
                return;
            }

            var transcribedText = transcriptionResult.Text;

            if (string.IsNullOrWhiteSpace(transcribedText))
            {
                this.Logger.LogWarning("The transcription result is empty.");
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, this.T("The transcription result is empty.")));
                return;
            }
            
            // Remove trailing and leading whitespace:
            transcribedText = transcribedText.Trim();
            
            // Replace line breaks with spaces:
            transcribedText = transcribedText.Replace("\r", " ").Replace("\n", " ");
            
            // Replace two spaces with a single space:
            transcribedText = transcribedText.Replace("  ", " ");

            this.Logger.LogInformation("Transcription completed successfully. Result length: {Length} characters.", transcribedText.Length);

            try
            {
                // Play the transcription done sound effect:
                await this.JsRuntime.InvokeVoidAsync("playSound", "/sounds/transcription_done.ogg");
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to play transcription done sound effect.");
            }

            // Copy the transcribed text to the clipboard:
            await this.RustService.CopyText2Clipboard(this.Snackbar, transcribedText);

        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "An error occurred during transcription.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, this.T("An error occurred during transcription.")));
        }
        finally
        {
            await this.ReleaseMicrophoneAsync();
            this.DeleteFinalRecording();
            this.isTranscribing = false;
            this.StateHasChanged();
        }
    }

    private void DeleteFinalRecording()
    {
        var recordingPath = this.finalRecordingPath;
        this.finalRecordingPath = null;

        if (recordingPath is null)
            return;

        try
        {
            if (File.Exists(recordingPath))
                File.Delete(recordingPath);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to delete the recording file '{RecordingPath}'.", recordingPath);
        }
    }

    private async Task ReleaseMicrophoneAsync()
    {
        // Wait a moment for any queued sounds to finish playing, then release the microphone.
        // This allows Bluetooth headsets to switch back to A2DP profile without interrupting audio:
        await Task.Delay(1_800);

        try
        {
            await this.JsRuntime.InvokeVoidAsync("audioRecorder.releaseMicrophone");
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to release the microphone.");
        }
    }

    private async Task<bool> EnsureSoundEffectsAvailableAsync(string context)
    {
        if (!this.ShouldRenderVoiceRecording)
            return false;

        if (!this.VoiceRecordingAvailabilityService.IsAvailable)
            return false;

        try
        {
            var result = await this.JsRuntime.InvokeAsync<SoundEffectsInitializationResult>("initSoundEffects");
            if (result.Success)
                return true;

            var failureDetails = BuildSoundEffectsFailureDetails(result);
            this.Logger.LogError("Failed to initialize sound effects {Context}. {FailureDetails}", context, failureDetails);
            await this.DisableVoiceRecordingAsync(failureDetails);
        }
        catch (JSDisconnectedException ex)
        {
            this.Logger.LogError(ex, "Failed to initialize sound effects {Context}. The JS runtime disconnected.", context);
            await this.DisableVoiceRecordingAsync("The JS runtime disconnected while initializing audio playback.");
        }
        catch (OperationCanceledException ex)
        {
            this.Logger.LogError(ex, "Failed to initialize sound effects {Context}. The interop call was canceled.", context);
            await this.DisableVoiceRecordingAsync("The interop call for audio playback initialization was canceled.");
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to initialize sound effects {Context}.", context);
            await this.DisableVoiceRecordingAsync(ex.Message);
        }

        return false;
    }

    private async Task DisableVoiceRecordingAsync(string reason)
    {
        if (!this.VoiceRecordingAvailabilityService.TryDisable(reason))
            return;

        this.Logger.LogWarning("Voice recording was disabled for the current session. Reason: {Reason}", reason);
        await this.MessageBus.SendWarning(new(Icons.Material.Filled.MicOff, this.T("Voice recording has been disabled for this session because audio playback could not be initialized on the client.")));
        await this.SendMessage(Event.VOICE_RECORDING_AVAILABILITY_CHANGED, reason);
        this.StateHasChanged();
    }

    private static string BuildSoundEffectsFailureDetails(SoundEffectsInitializationResult result)
    {
        var details = new List<string>();
        if (result.FailedPaths.Length > 0)
            details.Add($"Failed sound files: {string.Join(", ", result.FailedPaths)}.");

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            details.Add($"Client error: {result.ErrorMessage}");

        return details.Count > 0
            ? string.Join(" ", details)
            : "The client did not provide additional details.";
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.GlobalShortcutService.RuntimeStateChanged -= this.OnShortcutRuntimeStateChanged;

        if (this.localShortcutInteropReady)
            _ = this.JsRuntime.InvokeVoidAsync("localShortcut.unregister", "voice-recording-toggle");

        this.localShortcutDotNetReference?.Dispose();
        this.localShortcutDotNetReference = null;
        this.localShortcutInteropReady = false;

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
