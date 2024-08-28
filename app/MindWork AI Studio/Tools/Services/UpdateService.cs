using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools.Services;

public sealed class UpdateService : BackgroundService, IMessageBusReceiver
{
    private static bool IS_INITIALIZED;
    private static ISnackbar? SNACKBAR;
    
    private readonly SettingsManager settingsManager;
    private readonly MessageBus messageBus;
    private readonly RustService rust;
    
    private TimeSpan updateInterval;
    
    public UpdateService(MessageBus messageBus, SettingsManager settingsManager, RustService rust)
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

        this.updateInterval = this.settingsManager.ConfigurationData.App.UpdateBehavior switch
        {
            UpdateBehavior.NO_CHECK => Timeout.InfiniteTimeSpan,
            UpdateBehavior.ONCE_STARTUP => Timeout.InfiniteTimeSpan,
            
            UpdateBehavior.HOURLY => TimeSpan.FromHours(1),
            UpdateBehavior.DAILY => TimeSpan.FromDays(1),
            UpdateBehavior.WEEKLY => TimeSpan.FromDays(7),
            
            _ => TimeSpan.FromHours(1)
        };
        
        if(this.settingsManager.ConfigurationData.App.UpdateBehavior is UpdateBehavior.NO_CHECK)
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
    
    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
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
        
        var response = await this.rust.CheckForUpdate();
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
    
    public static void SetBlazorDependencies(ISnackbar snackbar)
    {
        SNACKBAR = snackbar;
        IS_INITIALIZED = true;
    }
}