using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;

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

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult<TResult?>(default);

    #endregion

    private async Task RegisterAllShortcuts()
    {
        this.logger.LogInformation("Registering global shortcuts.");
        foreach (var shortcutId in Enum.GetValues<Shortcut>())
        {
            if(shortcutId is Shortcut.NONE)
                continue;
            
            var shortcut = this.GetShortcutValue(shortcutId);
            var isEnabled = this.IsShortcutAllowed(shortcutId);

            if (isEnabled && !string.IsNullOrWhiteSpace(shortcut))
            {
                var success = await this.rustService.UpdateGlobalShortcut(shortcutId, shortcut);
                if (success)
                    this.logger.LogInformation("Global shortcut '{ShortcutId}' ({Shortcut}) registered.", shortcutId, shortcut);
                else
                    this.logger.LogWarning("Failed to register global shortcut '{ShortcutId}' ({Shortcut}).", shortcutId, shortcut);
            }
            else
            {
                // Disable the shortcut when empty or feature is disabled:
                await this.rustService.UpdateGlobalShortcut(shortcutId, string.Empty);
            }
        }

        this.logger.LogInformation("Global shortcuts registration completed.");
    }

    private string GetShortcutValue(Shortcut name) => name switch
    {
        Shortcut.VOICE_RECORDING_TOGGLE => this.settingsManager.ConfigurationData.App.ShortcutVoiceRecording,
        
        _ => string.Empty,
    };

    private bool IsShortcutAllowed(Shortcut name) => name switch
    {
        // Voice recording is a preview feature:
        Shortcut.VOICE_RECORDING_TOGGLE => PreviewFeatures.PRE_SPEECH_TO_TEXT_2026.IsEnabled(this.settingsManager),
        
        // Other shortcuts are always allowed:
        _ => true,
    };

    public static void Initialize() => IS_INITIALIZED = true;
}
