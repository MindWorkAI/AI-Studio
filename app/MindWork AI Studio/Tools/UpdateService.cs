using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public sealed class UpdateService : BackgroundService, IMessageBusReceiver
{
    // We cannot inject IJSRuntime into our service. This is due to the fact that
    // the service is not a Blaozor component. We need to pass the IJSRuntime from
    // the MainLayout component to the service.
    private static IJSRuntime? JS_RUNTIME;
    private static bool IS_INITALIZED;
    
    private readonly SettingsManager settingsManager;
    private readonly TimeSpan updateInterval;
    private readonly MessageBus messageBus;
    private readonly Rust rust;
    
    public UpdateService(MessageBus messageBus, SettingsManager settingsManager, Rust rust)
    {
        this.settingsManager = settingsManager;
        this.messageBus = messageBus;
        this.rust = rust;
        
        this.messageBus.RegisterComponent(this);
        this.ApplyFilters([], [ Event.USER_SEARCH_FOR_UPDATE ]);
        
        this.updateInterval = settingsManager.ConfigurationData.UpdateBehavior switch
        {
            UpdateBehavior.NO_CHECK => Timeout.InfiniteTimeSpan,
            UpdateBehavior.ONCE_STARTUP => Timeout.InfiniteTimeSpan,
            
            UpdateBehavior.HOURLY => TimeSpan.FromHours(1),
            UpdateBehavior.DAILY => TimeSpan.FromDays(1),
            UpdateBehavior.WEEKLY => TimeSpan.FromDays(7),
            
            _ => TimeSpan.FromHours(1)
        };
    }
    
    #region Overrides of BackgroundService

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !IS_INITALIZED)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        await this.settingsManager.LoadSettings();
        if(this.settingsManager.ConfigurationData.UpdateBehavior != UpdateBehavior.NO_CHECK)
            await this.CheckForUpdate();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(this.updateInterval, stoppingToken);
            await this.CheckForUpdate();
        }
    }

    #endregion

    #region Implementation of IMessageBusReceiver

    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.USER_SEARCH_FOR_UPDATE:
                await this.CheckForUpdate();
                break;
        }
    }

    #endregion

    #region Overrides of BackgroundService

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.messageBus.Unregister(this);
        await base.StopAsync(cancellationToken);
    }

    #endregion

    private async Task CheckForUpdate()
    {
        if(!IS_INITALIZED)
            return;
        
        var response = await this.rust.CheckForUpdate(JS_RUNTIME!);
        if (response.UpdateIsAvailable)
        {
            await this.messageBus.SendMessage(null, Event.UPDATE_AVAILABLE, response);
        }
    }
    
    public static void SetJsRuntime(IJSRuntime jsRuntime)
    {
        JS_RUNTIME = jsRuntime;
        IS_INITALIZED = true;
    }
}