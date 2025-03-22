using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class AssistantBlock<TSettings> : ComponentBase, IMessageBusReceiver, IDisposable where TSettings : IComponent
{
    [Parameter]
    public string Name { get; set; } = string.Empty;
    
    [Parameter]
    public string Description { get; set; } = string.Empty;
    
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.DisabledByDefault;
    
    [Parameter]
    public string ButtonText { get; set; } = "Start";
    
    [Parameter]
    public string Link { get; set; } = string.Empty;
    
    [Inject]
    private MudTheme ColorTheme { get; init; } = null!;
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    private MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    private async Task OpenSettingsDialog()
    {
        var dialogParameters = new DialogParameters();
        
        await this.DialogService.ShowAsync<TSettings>("Open Settings", dialogParameters, DialogOptions.FULLSCREEN);
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.COLOR_THEME_CHANGED ]);
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    #region Implementation of IMessageBusReceiver

    public string ComponentName => nameof(AssistantBlock<TSettings>);
    
    public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.COLOR_THEME_CHANGED:
                this.StateHasChanged();
                break;
        }
        
        return Task.CompletedTask;
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion
    
    private string BorderColor => this.SettingsManager.IsDarkMode switch
    {
        true => this.ColorTheme.GetCurrentPalette(this.SettingsManager).GrayLight,
        false => this.ColorTheme.GetCurrentPalette(this.SettingsManager).Primary.Value,
    };

    private string BlockStyle => $"border-width: 2px; border-color: {this.BorderColor}; border-radius: 12px; border-style: solid; max-width: 20em;";
    
    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
    }

    #endregion
}