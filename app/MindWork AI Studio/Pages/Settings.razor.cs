using AIStudio.Components;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Pages;

public partial class Settings : MSGComponentBase
{
    private List<ConfigurationSelectData<string>> availableLLMProviders = new();
    private List<ConfigurationSelectData<string>> availableEmbeddingProviders = new();

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        
        await base.OnInitializedAsync();
    }

    #endregion

    #region Overrides of MSGComponentBase
    
    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                this.StateHasChanged();
                break;
        }
        
        return Task.CompletedTask;
    }

    #endregion
}