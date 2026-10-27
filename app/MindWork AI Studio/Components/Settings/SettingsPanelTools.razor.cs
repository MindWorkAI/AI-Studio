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

    private async Task OpenSettings(ToolCatalogItem item)
    {
        var parameters = new DialogParameters<ToolSettingsDialog>
        {
            { x => x.CollectionId, item.Id },
        };

        var dialog = await this.DialogService.ShowAsync<ToolSettingsDialog>(null, parameters, Dialogs.DialogOptions.FULLSCREEN);
        await dialog.Result;
        this.items = await this.ToolRegistry.GetCatalogAsync(this.ToolRegistry.GetAllDefinitions());
        this.StateHasChanged();
    }

    private async Task OpenExport(ToolCatalogItem item)
    {
        if (!this.SettingsManager.ConfigurationData.App.ShowAdminSettings)
            return;

        var parameters = new DialogParameters<ToolSettingsExportDialog>
        {
            { x => x.CollectionId, item.Id },
        };

        await this.DialogService.ShowAsync<ToolSettingsExportDialog>(null, parameters, Dialogs.DialogOptions.FULLSCREEN);
    }

    private static bool IsSwitchedOffByUser(ToolCatalogItem item) => item.SwitchOffReason is ToolOfferBlockReason.TOOL_SWITCHED_OFF_BY_USER;

    /// <summary>
    /// Whether the user cannot switch this tool on or off, because their organization switched it or all tools off.
    /// </summary>
    private static bool IsSwitchLocked(ToolCatalogItem item) => item.SwitchOffReason is ToolOfferBlockReason.TOOLS_SWITCHED_OFF or ToolOfferBlockReason.TOOL_SWITCHED_OFF;

    private string GetSwitchTooltip(ToolCatalogItem item)
    {
        if (IsSwitchLocked(item))
            return this.T("This tool has been disabled by your organization.");

        return IsSwitchedOffByUser(item)
            ? this.T("Switch this tool on to use it again.")
            : this.T("Switch this tool off if you never need it. It then appears in no tool selection, and the AI cannot use it.");
    }

    private async Task SwitchToolOnOrOff(ToolCatalogItem item, bool isOn)
    {
        if (IsSwitchLocked(item))
            return;

        this.ToolRegistry.SetSwitchedOffByUser(item.Id, !isOn);
        await this.SettingsManager.StoreSettings();
        this.items = await this.ToolRegistry.GetCatalogAsync(this.ToolRegistry.GetAllDefinitions());

        // Tool selections which are open right now load their list anew:
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private string GetConfigurationTooltip(ToolCatalogItem item) => item.ConfigurationState.MissingRequiredFields.Count switch
    {
        _ when !string.IsNullOrWhiteSpace(item.ConfigurationState.Message) => item.ConfigurationState.Message,
        0 => this.T("This tool still needs to be configured."),
        _ => string.Format(this.T("Missing required settings: {0}"), string.Join(", ", item.ConfigurationState.MissingRequiredFields.Select(fieldName => this.GetFieldDisplayName(item, fieldName))))
    };

    /// <remarks>
    /// The missing fields are those of the first tool whose settings are incomplete, see
    /// ToolCatalogItem.ConfigurationState, so that tool names them.
    /// </remarks>
    private string GetFieldDisplayName(ToolCatalogItem item, string fieldName)
    {
        var tool = item.Tools.FirstOrDefault(tool => !tool.ConfigurationState.IsConfigured);
        var fieldDefinition = tool?.Definition.SettingsSchema.Properties.GetValueOrDefault(fieldName);
        if (tool is null || fieldDefinition is null)
            return fieldName;

        return tool.Implementation.GetSettingsFieldLabel(fieldName, fieldDefinition);
    }

    private IEnumerable<ConfidenceLevel> GetSelectableConfidenceLevels() =>
        Enum.GetValues<ConfidenceLevel>().OrderBy(x => x).Where(x => x is not ConfidenceLevel.UNKNOWN);

    private string GetCurrentConfidenceLevelName(ToolCatalogItem item) => this.GetConfidenceLevelName(GetMinimumProviderConfidence(item));

    // Short, because it labels the button in a narrow column whose heading already names the
    // minimum confidence; the long wording wraps into a tall block there:
    private string GetConfidenceLevelName(ConfidenceLevel confidenceLevel) => confidenceLevel is ConfidenceLevel.NONE
        ? this.T("No minimum")
        : confidenceLevel.GetName();

    private string SetCurrentConfidenceLevelColorStyle(ToolCatalogItem item) =>
        $"background-color: {GetMinimumProviderConfidence(item).GetColor(this.SettingsManager)};";

    private bool IsToolConfidenceManaged() =>
        ManagedConfiguration.TryGet(x => x.Tools, x => x.MinimumProviderConfidenceByToolId, out var meta) && meta.IsLocked;

    // The catalog already carries the resolved level, so there is nothing to look up again:
    private static ConfidenceLevel GetMinimumProviderConfidence(ToolCatalogItem item) => item.MinimumProviderConfidence;

    private async Task ChangeMinimumProviderConfidence(ToolCatalogItem item, ConfidenceLevel confidenceLevel)
    {
        this.ToolRegistry.SetMinimumProviderConfidence(item.Id, confidenceLevel);
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
