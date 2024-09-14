using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfigurationProviderSelection : ComponentBase, IMessageBusReceiver, IDisposable
{
    [Parameter]
    public Func<string> SelectedValue { get; set; } = () => string.Empty;
    
    [Parameter]
    public Action<string> SelectionUpdate { get; set; } = _ => { };

    [Parameter]
    public IEnumerable<ConfigurationSelectData<string>> Data { get; set; } = new List<ConfigurationSelectData<string>>();
    
    /// <summary>
    /// Is the selection component disabled?
    /// </summary>
    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;
    
    [Parameter]
    public Func<string> HelpText { get; set; } = () => "Select a provider that is preselected.";

    [Parameter]
    public Tools.Components Component { get; set; } = Tools.Components.NONE;
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    private MessageBus MessageBus { get; init; } = null!;

    #region Overrides of ComponentBase

    protected override async Task OnParametersSetAsync()
    {
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.CONFIGURATION_CHANGED ]);

        await base.OnParametersSetAsync();
    }

    #endregion
    
    private IEnumerable<ConfigurationSelectData<string>> FilteredData()
    {
        var minimumLevel = this.SettingsManager.GetMinimumConfidenceLevel(this.Component);
        foreach (var providerId in this.Data)
        {
            var provider = this.SettingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == providerId.Value);
            if (provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level >= minimumLevel)
                yield return providerId;
        }
    }
    
    #region Implementation of IMessageBusReceiver

    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                
                if(string.IsNullOrWhiteSpace(this.SelectedValue()))
                    break;
                
                // Check if the selected value is still valid:
                if (this.Data.All(x => x.Value != this.SelectedValue()))
                {
                    this.SelectedValue = () => string.Empty;
                    this.SelectionUpdate(string.Empty);
                    await this.SettingsManager.StoreSettings();
                }
                
                this.StateHasChanged();
                break;
        }
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion

    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
    }

    #endregion
}