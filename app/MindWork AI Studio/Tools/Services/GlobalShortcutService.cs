using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools.Services;

public sealed class GlobalShortcutService : BackgroundService, IMessageBusReceiver
{
    private static bool IS_INITIALIZED;

    private readonly ILogger<GlobalShortcutService> logger;
    private readonly SettingsManager settingsManager;
    private readonly MessageBus messageBus;
    private readonly RustService rustService;

    public GlobalShortcutService(
        ILogger<GlobalShortcutService> logger,
        SettingsManager settingsManager,
        MessageBus messageBus,
        RustService rustService)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.messageBus = messageBus;
        this.rustService = rustService;

        this.messageBus.RegisterComponent(this);
        this.ApplyFilters([], [Event.CONFIGURATION_CHANGED]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait until the app is fully initialized:
        while (!stoppingToken.IsCancellationRequested && !IS_INITIALIZED)
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        // Register shortcuts on startup:
        await this.RegisterAllShortcuts();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.messageBus.Unregister(this);
        await base.StopAsync(cancellationToken);
    }

    #region IMessageBusReceiver
    
    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                await this.RegisterAllShortcuts();
                break;
        }
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(
        ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
        => Task.FromResult<TResult?>(default);

    #endregion

    private async Task RegisterAllShortcuts()
    {
        this.logger.LogInformation("Registering global shortcuts.");
        
        //
        // Voice recording shortcut (preview feature)
        //
        if (PreviewFeatures.PRE_SPEECH_TO_TEXT_2026.IsEnabled(this.settingsManager))
        {
            var shortcut = this.settingsManager.ConfigurationData.App.ShortcutVoiceRecording;
            if (!string.IsNullOrWhiteSpace(shortcut))
            {
                var success = await this.rustService.UpdateGlobalShortcut("voice_recording_toggle", shortcut);
                if (success)
                    this.logger.LogInformation("Global shortcut 'voice_recording_toggle' ({Shortcut}) registered.", shortcut);
                else
                    this.logger.LogWarning("Failed to register global shortcut 'voice_recording_toggle' ({Shortcut}).", shortcut);
            }
            else
            {
                // Disable shortcut when empty
                await this.rustService.UpdateGlobalShortcut("voice_recording_toggle", string.Empty);
            }
        }
        else
        {
            // Disable the shortcut when the preview feature is disabled:
            await this.rustService.UpdateGlobalShortcut("voice_recording_toggle", string.Empty);
        }
        
        this.logger.LogInformation("Global shortcuts registration completed.");
    }

    public static void Initialize() => IS_INITIALIZED = true;
}
