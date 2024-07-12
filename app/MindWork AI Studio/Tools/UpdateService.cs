using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public sealed class UpdateService : BackgroundService, IMessageBusReceiver
{
    // We cannot inject IJSRuntime into our service. This is because
    // the service is not a Blazor component. We need to pass the IJSRuntime from
    // the MainLayout component to the service.
    private static IJSRuntime? JS_RUNTIME;
    private static bool IS_INITIALIZED;
    private static ISnackbar? SNACKBAR;
    
    private readonly SettingsManager settingsManager;
    private readonly MessageBus messageBus;
    private readonly Rust rust;
    
    private TimeSpan updateInterval;
    
    public UpdateService(MessageBus messageBus, SettingsManager settingsManager, Rust rust)
    {
        this.settingsManager = settingsManager;
        this.messageBus = messageBus;
        this.rust = rust;

        this.messageBus.RegisterComponent(this);
        this.ApplyFilters([], [ Event.USER_SEARCH_FOR_UPDATE ]);
    }
    
    #region Overrides of BackgroundService

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !IS_INITIALIZED)
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        this.updateInterval = this.settingsManager.ConfigurationData.UpdateBehavior switch
        {
            UpdateBehavior.NO_CHECK => Timeout.InfiniteTimeSpan,
            UpdateBehavior.ONCE_STARTUP => Timeout.InfiniteTimeSpan,
            
            UpdateBehavior.HOURLY => TimeSpan.FromHours(1),
            UpdateBehavior.DAILY => TimeSpan.FromDays(1),
            UpdateBehavior.WEEKLY => TimeSpan.FromDays(7),
            
            _ => TimeSpan.FromHours(1)
        };
        
        if(this.settingsManager.ConfigurationData.UpdateBehavior is UpdateBehavior.NO_CHECK)
            return;
        
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
                await this.CheckForUpdate(notifyUserWhenNoUpdate: true);
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

    private async Task CheckForUpdate(bool notifyUserWhenNoUpdate = false)
    {
        if(!IS_INITIALIZED)
            return;
        
        var response = await this.rust.CheckForUpdate(JS_RUNTIME!);
        if (response.UpdateIsAvailable)
        {
            await this.messageBus.SendMessage(null, Event.UPDATE_AVAILABLE, response);
        }
        else
        {
            if (notifyUserWhenNoUpdate)
            {
                SNACKBAR!.Add("No update found.", Severity.Normal, config =>
                {
                    config.Icon = Icons.Material.Filled.Update;
                    config.IconSize = Size.Large;
                    config.IconColor = Color.Primary;
                });
            }
        }
    }
    
    public static void SetBlazorDependencies(IJSRuntime jsRuntime, ISnackbar snackbar)
    {
        SNACKBAR = snackbar;
        JS_RUNTIME = jsRuntime;
        IS_INITIALIZED = true;
    }
}