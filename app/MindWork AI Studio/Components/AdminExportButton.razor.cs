using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The common admin-only configuration export action. Callers decide what is exported.
/// </summary>
public partial class AdminExportButton : MSGComponentBase
{
    [Parameter]
    public EventCallback OnClick { get; set; }

    [Parameter]
    public Variant Variant { get; set; } = Variant.Text;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
    }

    private async Task Export()
    {
        if (this.SettingsManager.ConfigurationData.App.ShowAdminSettings)
            await this.OnClick.InvokeAsync();
    }

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED)
            this.StateHasChanged();

        return Task.CompletedTask;
    }
}