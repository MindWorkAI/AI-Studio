using AIStudio.Settings;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools.Services;

/// <summary>
/// Hands the interface sizes the user chose to the runtime, which zooms the webview accordingly.
/// </summary>
/// <remarks>
/// The runtime shows every screen in its default size from the moment the window opens, keeps the
/// sizes, measures the screen again whenever the window moves to another one, and applies the size of
/// that screen. This service only tells it what the user chose: as soon as the settings have loaded,
/// and again whenever they change.
/// </remarks>
public sealed class InterfaceSizeService : BackgroundService, IMessageBusReceiver
{
    /// <summary>
    /// The smallest custom scale on any screen, in percent. A screen may allow less of the range,
    /// which the runtime reports. Keep in sync with MIN_SCALE in runtime/src/interface_zoom.rs.
    /// </summary>
    private const int MIN_CUSTOM_PERCENT = 60;

    /// <summary>
    /// The largest custom scale on any screen, in percent. A screen may allow less of the range,
    /// which the runtime reports. Keep in sync with MAX_SCALE in runtime/src/interface_zoom.rs.
    /// </summary>
    private const int MAX_CUSTOM_PERCENT = 300;

    /// <summary>
    /// How often the service looks whether the settings have loaded. Short, because a screen with a
    /// size of its own shows in its default size until then.
    /// </summary>
    private static readonly TimeSpan SETTINGS_LOAD_CHECK_INTERVAL = TimeSpan.FromMilliseconds(100);

    private readonly SemaphoreSlim sendingSemaphore = new(1, 1);
    private readonly ILogger<InterfaceSizeService> logger;
    private readonly SettingsManager settingsManager;
    private readonly MessageBus messageBus;
    private readonly RustService rustService;
    private string? lastSentSignature;
    private volatile bool areSettingsLoaded;

    public InterfaceSizeService(ILogger<InterfaceSizeService> logger, SettingsManager settingsManager, MessageBus messageBus, RustService rustService)
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
        this.logger.LogInformation("The interface size service was initialized.");

        //
        // The app loads the settings early, long before its startup completed with the plugins. The
        // sizes go to the runtime right then, so that a screen with a size of its own changes from
        // its default size as soon as possible:
        //
        while (!this.settingsManager.HasCompletedInitialSettingsLoad && !stoppingToken.IsCancellationRequested)
            await Task.Delay(SETTINGS_LOAD_CHECK_INTERVAL, stoppingToken);

        this.areSettingsLoaded = true;
        await this.SendInterfaceSize();
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.messageBus.Unregister(this);
        await base.StopAsync(cancellationToken);
    }

    #region IMessageBusReceiver

    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        // Until the settings have loaded, ExecuteAsync waits to send them:
        if (triggeredEvent is Event.CONFIGURATION_CHANGED && this.areSettingsLoaded)
            await this.SendInterfaceSize();
    }

    public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult(MessageBusAnswer<TResult>.NONE);

    #endregion

    /// <summary>
    /// Sends the chosen sizes to the runtime, unless the runtime has them already.
    /// </summary>
    /// <remarks>
    /// Every change of any setting announces itself the same way, so most of them leave the sizes
    /// as they were. A scale beyond the range of any screen, e.g., in settings edited by hand, is
    /// brought within that range here, and the runtime narrows it to the screen.
    /// </remarks>
    private async Task SendInterfaceSize()
    {
        var screens = this.settingsManager.ConfigurationData.App.InterfaceScreenSizes
            .DistinctBy(screen => screen.ScreenKey)
            .Select(screen => screen with { CustomPercent = screen.CustomPercent is { } percent ? Math.Clamp(percent, MIN_CUSTOM_PERCENT, MAX_CUSTOM_PERCENT) : null })
            .ToList();

        // The order of the screens tells when they were set, which the runtime does not need to know:
        var signature = string.Join(";", screens.OrderBy(screen => screen.ScreenKey, StringComparer.Ordinal).Select(screen => $"{screen.ScreenKey}={screen.Size}:{screen.CustomPercent}"));

        await this.sendingSemaphore.WaitAsync();
        try
        {
            if (signature == this.lastSentSignature)
                return;

            if (await this.rustService.SetInterfaceSize(new SetInterfaceSizeRequest(screens)) is not null)
                this.lastSentSignature = signature;
        }
        finally
        {
            this.sendingSemaphore.Release();
        }
    }
}