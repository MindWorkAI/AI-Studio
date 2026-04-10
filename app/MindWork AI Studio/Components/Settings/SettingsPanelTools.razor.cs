using AIStudio.Dialogs.Settings;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelTools : SettingsPanelBase
{
    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    private IReadOnlyList<ToolCatalogItem> items = [];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.items = await this.ToolRegistry.GetCatalogAsync(this.ToolRegistry.GetAllDefinitions());
    }

    private async Task OpenSettings(string toolId)
    {
        var parameters = new DialogParameters<ToolSettingsDialog>
        {
            { x => x.ToolId, toolId },
        };

        var dialog = await this.DialogService.ShowAsync<ToolSettingsDialog>(null, parameters, Dialogs.DialogOptions.FULLSCREEN);
        await dialog.Result;
        this.items = await this.ToolRegistry.GetCatalogAsync(this.ToolRegistry.GetAllDefinitions());
        this.StateHasChanged();
    }

    private string GetConfigurationTooltip(ToolCatalogItem item) => item.ConfigurationState.MissingRequiredFields.Count switch
    {
        0 => this.T("This tool still needs to be configured."),
        _ => string.Format(this.T("Missing required settings: {0}"), string.Join(", ", item.ConfigurationState.MissingRequiredFields.Select(fieldName => this.GetFieldDisplayName(item, fieldName))))
    };

    private string GetFieldDisplayName(ToolCatalogItem item, string fieldName)
    {
        var fieldDefinition = item.Definition.SettingsSchema.Properties.GetValueOrDefault(fieldName);
        if (fieldDefinition is null)
            return fieldName;

        return item.Implementation.GetSettingsFieldLabel(fieldName, fieldDefinition);
    }
}
