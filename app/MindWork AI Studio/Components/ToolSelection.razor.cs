using AIStudio.Dialogs.Settings;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ToolSelection : MSGComponentBase
{
    [Parameter]
    public AIStudio.Tools.Components Component { get; set; } = AIStudio.Tools.Components.CHAT;

    [Parameter]
    public required AIStudio.Settings.Provider LLMProvider { get; set; }

    [Parameter]
    public HashSet<string> SelectedToolIds { get; set; } = [];

    [Parameter]
    public EventCallback<HashSet<string>> SelectedToolIdsChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string PopoverButtonClasses { get; set; } = string.Empty;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    private bool showSelection;
    private IReadOnlyList<ToolCatalogItem> catalog = [];

    protected override void OnParametersSet()
    {
        this.SelectedToolIds = ToolSelectionRules.NormalizeSelection(this.SelectedToolIds);
        base.OnParametersSet();
    }

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    private bool SupportsTools =>
        this.LLMProvider != AIStudio.Settings.Provider.NONE &&
        this.LLMProvider.GetModelCapabilities().Contains(Capability.CHAT_COMPLETION_API) &&
        this.LLMProvider.GetModelCapabilities().Contains(Capability.FUNCTION_CALLING);

    private ConfidenceLevel ProviderConfidence => this.LLMProvider == AIStudio.Settings.Provider.NONE
        ? ConfidenceLevel.NONE
        : this.LLMProvider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level;

    private async Task ToggleSelection()
    {
        this.showSelection = !this.showSelection;
        if (this.showSelection)
            this.catalog = await this.ToolRegistry.GetCatalogAsync(this.Component);
    }

    private void Hide() => this.showSelection = false;

    private async Task ChangeSelection(string toolId, bool isSelected)
    {
        var updated = new HashSet<string>(this.SelectedToolIds, StringComparer.Ordinal);
        if (isSelected)
            updated.Add(toolId);
        else
            updated.Remove(toolId);

        updated = ToolSelectionRules.NormalizeSelection(updated);
        this.SelectedToolIds = updated;
        await this.SelectedToolIdsChanged.InvokeAsync(updated);
    }

    private bool IsSelectionLockedByDependency(string toolId) => ToolSelectionRules.IsRequiredBySelectedTools(toolId, this.SelectedToolIds);

    private ConfidenceLevel GetMinimumProviderConfidence(ToolCatalogItem item) => this.SettingsManager.GetMinimumProviderConfidenceForTool(item.Definition.Id);

    private bool IsBlockedByProviderConfidence(ToolCatalogItem item) => !ToolSelectionRules.IsProviderConfidenceAllowed(this.ProviderConfidence, this.GetMinimumProviderConfidence(item));

    private string? GetDependencyHint(string toolId)
    {
        if (toolId == ToolSelectionRules.WEB_SEARCH_TOOL_ID)
            return this.T("Enabling this tool also enables Read Web Page.");

        if (this.IsSelectionLockedByDependency(toolId))
            return this.T("This tool is currently required because Web Search is enabled.");

        return null;
    }

    private string? GetProviderConfidenceHint(ToolCatalogItem item)
    {
        if (!this.IsBlockedByProviderConfidence(item))
            return null;

        return string.Format(
            this.T("This tool requires provider confidence {0}. The selected provider has {1}."),
            this.GetMinimumProviderConfidence(item).GetName(),
            this.ProviderConfidence.GetName());
    }

    private async Task OpenSettings(string toolId)
    {
        var parameters = new DialogParameters<ToolSettingsDialog>
        {
            { x => x.ToolId, toolId },
        };

        var dialog = await this.DialogService.ShowAsync<ToolSettingsDialog>(null, parameters, Dialogs.DialogOptions.FULLSCREEN);
        await dialog.Result;
        this.catalog = await this.ToolRegistry.GetCatalogAsync(this.Component);
        this.StateHasChanged();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED when this.showSelection:
                this.catalog = await this.ToolRegistry.GetCatalogAsync(this.Component);
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }
}
