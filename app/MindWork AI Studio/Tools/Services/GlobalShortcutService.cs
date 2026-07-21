using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools.Services;

public sealed class GlobalShortcutService : BackgroundService, IMessageBusReceiver
{
    private static bool IS_STARTUP_COMPLETED;

    private enum ShortcutSyncSource
    {
        CONFIGURATION_CHANGED,
        STARTUP_COMPLETED,
        PLUGINS_RELOADED,
        VOICE_RECORDING_AVAILABILITY_CHANGED,
    }

    private readonly SemaphoreSlim registrationSemaphore = new(1, 1);
    private readonly object runtimeStateLock = new();
    private readonly Dictionary<Shortcut, ShortcutState> lastSentStates = [];
    private readonly Dictionary<Shortcut, string> lastNonEmptyShortcuts = [];
    private readonly Dictionary<Shortcut, ShortcutRuntimeBinding> runtimeBindings = [];
    private readonly ILogger<GlobalShortcutService> logger;
    private readonly SettingsManager settingsManager;
    private readonly MessageBus messageBus;
    private readonly RustService rustService;
    private readonly VoiceRecordingAvailabilityService voiceRecordingAvailabilityService;
    private bool isProcessingSuspended;
    private bool localFallbackWarningShown;

    public event Func<GlobalShortcutRuntimeState, Task>? RuntimeStateChanged;

    public GlobalShortcutService(
        ILogger<GlobalShortcutService> logger,
        SettingsManager settingsManager,
        MessageBus messageBus,
        RustService rustService,
        VoiceRecordingAvailabilityService voiceRecordingAvailabilityService)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.messageBus = messageBus;
        this.rustService = rustService;
        this.voiceRecordingAvailabilityService = voiceRecordingAvailabilityService;

        this.messageBus.RegisterComponent(this);
        this.ApplyFilters([], [Event.CONFIGURATION_CHANGED, Event.PLUGINS_RELOADED, Event.STARTUP_COMPLETED, Event.TAURI_EVENT_RECEIVED, Event.VOICE_RECORDING_AVAILABILITY_CHANGED]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("The global shortcut service was initialized.");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.messageBus.Unregister(this);
        this.registrationSemaphore.Dispose();
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the active backend and processing state for a shortcut.
    /// </summary>
    public GlobalShortcutRuntimeState GetRuntimeState(Shortcut shortcutId)
    {
        lock (this.runtimeStateLock)
        {
            if (this.runtimeBindings.TryGetValue(shortcutId, out var binding))
                return new(shortcutId, binding.Shortcut, binding.Backend, this.isProcessingSuspended);

            return new(shortcutId, string.Empty, ShortcutBackend.NONE, this.isProcessingSuspended);
        }
    }

    /// <summary>
    /// Pauses native and focused-window shortcut processing.
    /// </summary>
    public async Task<bool> SuspendShortcutProcessing()
    {
        lock (this.runtimeStateLock)
            this.isProcessingSuspended = true;

        await this.PublishAllRuntimeStates();
        return await this.rustService.SuspendShortcutProcessing();
    }

    /// <summary>
    /// Resumes native and focused-window shortcut processing.
    /// </summary>
    public async Task<bool> ResumeShortcutProcessing()
    {
        var result = await this.rustService.ResumeShortcutProcessing();
        lock (this.runtimeStateLock)
            this.isProcessingSuspended = false;

        await this.PublishAllRuntimeStates();
        return result;
    }

    #region IMessageBusReceiver
    
    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                if (!IS_STARTUP_COMPLETED)
                    return;

                await this.RegisterAllShortcuts(ShortcutSyncSource.CONFIGURATION_CHANGED);
                break;

            case Event.STARTUP_COMPLETED:
                IS_STARTUP_COMPLETED = true;
                await this.RegisterAllShortcuts(ShortcutSyncSource.STARTUP_COMPLETED);
                break;

            case Event.PLUGINS_RELOADED:
                if (!IS_STARTUP_COMPLETED)
                    return;

                await this.RegisterAllShortcuts(ShortcutSyncSource.PLUGINS_RELOADED);
                break;

            case Event.VOICE_RECORDING_AVAILABILITY_CHANGED:
                if (!IS_STARTUP_COMPLETED)
                    return;

                await this.RegisterAllShortcuts(ShortcutSyncSource.VOICE_RECORDING_AVAILABILITY_CHANGED);
                break;

            case Event.TAURI_EVENT_RECEIVED:
                if (data is TauriEvent tauriEvent
                    && tauriEvent.TryGetShortcutChange(out var shortcutId, out var effectiveDisplayName))
                {
                    await this.UpdateEffectiveDisplayName(shortcutId, effectiveDisplayName);
                }
                break;
        }
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult<TResult?>(default);

    #endregion

    private async Task RegisterAllShortcuts(ShortcutSyncSource source)
    {
        await this.registrationSemaphore.WaitAsync();
        try
        {
            this.logger.LogInformation("Registering global shortcuts (source='{Source}').", source);
            foreach (var shortcutId in Enum.GetValues<Shortcut>())
            {
                if(shortcutId is Shortcut.NONE)
                    continue;

                var shortcutState = await this.GetShortcutState(shortcutId, source);
                var shortcut = shortcutState.Shortcut;
                var isEnabled = shortcutState.IsEnabled;
                var requestedState = new ShortcutState(isEnabled ? shortcut : string.Empty, isEnabled, shortcutState.UsesPersistedFallback);
                this.logger.LogInformation(
                    "Sync shortcut '{ShortcutId}' (source='{Source}', enabled={IsEnabled}, configured='{Shortcut}').",
                    shortcutId,
                    source,
                    isEnabled,
                    shortcut);

                if (shortcutState.UsesPersistedFallback)
                {
                    this.logger.LogWarning(
                        "Using persisted shortcut fallback for '{ShortcutId}' during startup completion (source='{Source}', configured='{Shortcut}').",
                        shortcutId,
                        source,
                        shortcut);
                }

                if (this.lastSentStates.TryGetValue(shortcutId, out var lastSentState)
                    && lastSentState.Shortcut == requestedState.Shortcut
                    && lastSentState.IsEnabled == requestedState.IsEnabled)
                {
                    this.logger.LogDebug("Skipping unchanged global shortcut '{ShortcutId}'.", shortcutId);
                    continue;
                }

                var description = await this.GetShortcutDescription(shortcutId);
                var reconfigure = !string.IsNullOrWhiteSpace(requestedState.Shortcut)
                    && this.lastNonEmptyShortcuts.TryGetValue(shortcutId, out var lastNonEmptyShortcut)
                    && !string.Equals(lastNonEmptyShortcut, requestedState.Shortcut, StringComparison.Ordinal);
                
                var result = await this.rustService.UpdateGlobalShortcut(shortcutId, requestedState.Shortcut, description, reconfigure);
                if (result.Success)
                {
                    this.lastSentStates[shortcutId] = requestedState;
                    if (!string.IsNullOrWhiteSpace(requestedState.Shortcut))
                        this.lastNonEmptyShortcuts[shortcutId] = requestedState.Shortcut;

                    lock (this.runtimeStateLock)
                        this.runtimeBindings[shortcutId] = new(requestedState.Shortcut, result.Backend);

                    this.logger.LogInformation(
                        "Global shortcut '{ShortcutId}' ({Shortcut}) synchronized through {Backend}.",
                        shortcutId,
                        requestedState.Shortcut,
                        result.Backend);

                    if (result.Backend is ShortcutBackend.PORTAL)
                        await this.UpdateEffectiveDisplayName(shortcutId, result.EffectiveDisplayName);

                    await this.PublishRuntimeState(shortcutId);
                    if (result.Backend is ShortcutBackend.LOCAL && !this.localFallbackWarningShown)
                    {
                        this.localFallbackWarningShown = true;
                        await this.messageBus.SendWarning(new(
                            Icons.Material.Filled.Keyboard,
                            TB("The voice recording shortcut currently works only while AI Studio is focused.")));
                    }
                }
                else
                {
                    var userMessage = result.Cancelled
                        ? TB("The global shortcut change was cancelled. The previous shortcut remains active.")
                        : TB("The global shortcut could not be registered. The previous shortcut remains active.");
                    
                    this.logger.LogWarning(
                        "Failed to synchronize global shortcut '{ShortcutId}' ({Shortcut}, backend={Backend}, cancelled={Cancelled}): {Error}",
                        shortcutId,
                        requestedState.Shortcut,
                        result.Backend,
                        result.Cancelled,
                        result.ErrorMessage);

                    if (result.Cancelled)
                        await this.messageBus.SendWarning(new(Icons.Material.Filled.Keyboard, userMessage));
                    else
                        await this.messageBus.SendError(new(Icons.Material.Filled.Keyboard, userMessage));
                }
            }

            this.logger.LogInformation("Global shortcuts registration completed (source='{Source}').", source);
        }
        finally
        {
            this.registrationSemaphore.Release();
        }
    }

    private string GetShortcutValue(Shortcut name) => name switch
    {
        Shortcut.VOICE_RECORDING_TOGGLE => this.settingsManager.ConfigurationData.App.ShortcutVoiceRecording,
        
        _ => string.Empty,
    };

    private bool IsShortcutAllowed(Shortcut name) => name switch
    {
        // Voice recording is a preview feature:
        Shortcut.VOICE_RECORDING_TOGGLE => PreviewFeatures.PRE_SPEECH_TO_TEXT_2026.IsEnabled(this.settingsManager)
                                             && this.voiceRecordingAvailabilityService.IsAvailable,

        // Other shortcuts are always allowed:
        _ => true,
    };

    private async Task<string> GetShortcutDescription(Shortcut shortcutId)
    {
        var language = await this.settingsManager.GetActiveLanguagePlugin();
        return shortcutId switch
        {
            Shortcut.VOICE_RECORDING_TOGGLE => I18N.I.GetText(language, "Toggle voice recording", typeof(GlobalShortcutService).Namespace, nameof(GlobalShortcutService)),
            _ => I18N.I.GetText(language, "Global shortcut", typeof(GlobalShortcutService).Namespace, nameof(GlobalShortcutService)),
        };
    }

    private async Task UpdateEffectiveDisplayName(Shortcut shortcutId, string effectiveDisplayName)
    {
        if (shortcutId is not Shortcut.VOICE_RECORDING_TOGGLE || string.IsNullOrWhiteSpace(effectiveDisplayName))
            return;

        var configuredShortcut = this.settingsManager.ConfigurationData.App.ShortcutVoiceRecording;
        if (this.settingsManager.ConfigurationData.App.ShortcutVoiceRecordingDisplayName == effectiveDisplayName
            && this.settingsManager.ConfigurationData.App.ShortcutVoiceRecordingDisplaySource == configuredShortcut)
            return;

        this.settingsManager.ConfigurationData.App.ShortcutVoiceRecordingDisplayName = effectiveDisplayName;
        this.settingsManager.ConfigurationData.App.ShortcutVoiceRecordingDisplaySource = configuredShortcut;
        await this.settingsManager.StoreSettings();
        await this.messageBus.SendMessage<bool>(null, Event.GLOBAL_SHORTCUT_CHANGED);
    }

    private async Task PublishAllRuntimeStates()
    {
        Shortcut[] shortcutIds;
        lock (this.runtimeStateLock)
            shortcutIds = this.runtimeBindings.Keys.ToArray();

        foreach (var shortcutId in shortcutIds)
            await this.PublishRuntimeState(shortcutId);
    }

    private async Task PublishRuntimeState(Shortcut shortcutId)
    {
        var subscribers = this.RuntimeStateChanged;
        if (subscribers is null)
            return;

        var handlers = subscribers.GetInvocationList()
            .Cast<Func<GlobalShortcutRuntimeState, Task>>()
            .ToArray();
        var runtimeState = this.GetRuntimeState(shortcutId);

        foreach (var handler in handlers)
            await handler(runtimeState);
    }

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(GlobalShortcutService).Namespace, nameof(GlobalShortcutService));

    private async Task<ShortcutState> GetShortcutState(Shortcut shortcutId, ShortcutSyncSource source)
    {
        var shortcut = this.GetShortcutValue(shortcutId);
        var isEnabled = this.IsShortcutAllowed(shortcutId);
        if (isEnabled && !string.IsNullOrWhiteSpace(shortcut))
            return new(shortcut, true, false);

        if (source is not ShortcutSyncSource.STARTUP_COMPLETED || shortcutId is not Shortcut.VOICE_RECORDING_TOGGLE)
            return new(shortcut, isEnabled, false);

        var settingsSnapshot = await this.settingsManager.TryReadSettingsSnapshot();
        if (settingsSnapshot is null)
            return new(shortcut, isEnabled, false);

        var fallbackShortcut = settingsSnapshot.App.ShortcutVoiceRecording;
        var fallbackEnabled = !string.IsNullOrWhiteSpace(settingsSnapshot.App.UseTranscriptionProvider);

        if (!fallbackEnabled || string.IsNullOrWhiteSpace(fallbackShortcut))
            return new(shortcut, isEnabled, false);

        return new(fallbackShortcut, true, true);
    }

    private readonly record struct ShortcutState(string Shortcut, bool IsEnabled, bool UsesPersistedFallback);

    private readonly record struct ShortcutRuntimeBinding(string Shortcut, ShortcutBackend Backend);
}

public sealed record GlobalShortcutRuntimeState(
    Shortcut ShortcutId,
    string Shortcut,
    ShortcutBackend Backend,
    bool IsSuspended);