using AIStudio.Provider;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings;
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
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        this.items = await this.ToolRegistry.GetCatalogAsync(this.ToolRegistry.GetAllDefinitions());
        await base.OnInitializedAsync();
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

    private async Task OpenExport(string toolId)
    {
        if (!this.SettingsManager.ConfigurationData.App.ShowAdminSettings)
            return;

        var parameters = new DialogParameters<ToolSettingsExportDialog>
        {
            { x => x.ToolId, toolId },
        };

        await this.DialogService.ShowAsync<ToolSettingsExportDialog>(null, parameters, Dialogs.DialogOptions.FULLSCREEN);
    }

    private string GetConfigurationTooltip(ToolCatalogItem item) => item.ConfigurationState.MissingRequiredFields.Count switch
    {
        _ when !string.IsNullOrWhiteSpace(item.ConfigurationState.Message) => item.ConfigurationState.Message,
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

    private IEnumerable<ConfidenceLevel> GetSelectableConfidenceLevels() =>
        Enum.GetValues<ConfidenceLevel>().OrderBy(x => x).Where(x => x is not ConfidenceLevel.UNKNOWN);

    private string GetCurrentConfidenceLevelName(ToolCatalogItem item) => this.GetConfidenceLevelName(GetMinimumProviderConfidence(item));

    private string GetConfidenceLevelName(ConfidenceLevel confidenceLevel) => confidenceLevel is ConfidenceLevel.NONE
        ? this.T("No minimum confidence level chosen")
        : confidenceLevel.GetName();

    private string SetCurrentConfidenceLevelColorStyle(ToolCatalogItem item) =>
        $"background-color: {GetMinimumProviderConfidence(item).GetColor(this.SettingsManager)};";

    private bool IsToolConfidenceManaged() =>
        ManagedConfiguration.TryGet(x => x.Tools, x => x.MinimumProviderConfidenceByToolId, out var meta) && meta.IsLocked;

    // The catalog already carries the resolved level, so there is nothing to look up again:
    private static ConfidenceLevel GetMinimumProviderConfidence(ToolCatalogItem item) => item.MinimumProviderConfidence;

    private async Task ChangeMinimumProviderConfidence(ToolCatalogItem item, ConfidenceLevel confidenceLevel)
    {
        this.SettingsManager.SetMinimumProviderConfidenceForTool(item.Definition.Id, confidenceLevel, item.Definition.MinimumProviderConfidence);
        await this.SettingsManager.StoreSettings();
        this.items = await this.ToolRegistry.GetCatalogAsync(this.ToolRegistry.GetAllDefinitions());
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                this.items = await this.ToolRegistry.GetCatalogAsync(this.ToolRegistry.GetAllDefinitions());
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }
}
