using AIStudio.Dialogs.Settings;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ToolSelection : MSGComponentBase
{
    [Parameter]
    public AIStudio.Tools.Components Component { get; set; } = AIStudio.Tools.Components.CHAT;

    [Parameter]
    public required AIStudio.Settings.Provider LLMProvider { get; set; }

    /// <summary>
    /// Where the chat of this selection may still send data, because of the mailboxes it read from.
    /// </summary>
    /// <remarks>
    /// The selection shows a tool this keeps back as unavailable, with the reason, the same way it
    /// does for a provider with too little confidence. Keeping the tool out of the request is the
    /// business of the tool registry and the tool executor; this only explains it.
    /// </remarks>
    [Parameter]
    public required OutboundDataRequirement RequiredOutboundDataRestriction { get; set; }

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

    private ToolCallingAvailability ToolCallingAvailability => this.LLMProvider.GetToolCallingAvailability();

    private bool SupportsTools => this.ToolCallingAvailability.IsAvailable;

    private string ToolButtonTooltip => this.SupportsTools
        ? this.T("Select tools")
        : this.UnsupportedToolsMessage;

    private string UnsupportedToolsMessage => this.ToolCallingAvailability.Message;

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

    /// <summary>
    /// Whether this tool can be switched at all right now.
    /// </summary>
    /// <remarks>
    /// The switch and the row click share this, so both agree on when a tool is out of reach: the
    /// organization disabled it, it is not configured, the provider lacks the confidence it needs,
    /// the chat read a mailbox which keeps the tool back, a response is running, or the model
    /// cannot call tools in the first place.
    /// </remarks>
    private bool IsRowDisabled(ToolCatalogItem item) => !item.IsActive || !item.ConfigurationState.IsConfigured || this.IsBlockedByProviderConfidence(item) ||
                                                        this.IsBlockedByOutboundDataRestriction(item) || this.Disabled || !this.SupportsTools;

    /// <summary>
    /// Switches a tool when the user clicks anywhere in its row.
    /// </summary>
    /// <remarks>
    /// Hitting the switch itself is needless precision work, so the text, the icon, and the empty
    /// space count as well. Only the settings button is left out, because it sits outside the
    /// button that spans the rest of the row.
    /// </remarks>
    private async Task ToggleToolFromRow(ToolCatalogItem item)
    {
        if (this.IsRowDisabled(item))
            return;

        await this.ChangeSelection(item.Definition.Id, !this.SelectedToolIds.Contains(item.Definition.Id));
    }

    private async Task ChangeSelection(string toolId, bool isSelected)
    {
        if (isSelected && !this.SettingsManager.IsToolActive(toolId))
            return;

        var updated = new HashSet<string>(this.SelectedToolIds, StringComparer.Ordinal);
        if (isSelected)
            updated.Add(toolId);
        else
            updated.Remove(toolId);

        updated = ToolSelectionRules.NormalizeSelection(updated);
        this.SelectedToolIds = updated;
        await this.SelectedToolIdsChanged.InvokeAsync(updated);
    }

    // The catalog already carries the resolved level, so there is nothing to look up again:
    private static ConfidenceLevel GetMinimumProviderConfidence(ToolCatalogItem item) => item.MinimumProviderConfidence;

    private bool IsBlockedByProviderConfidence(ToolCatalogItem item) => !ToolSelectionRules.IsProviderConfidenceAllowed(this.ProviderConfidence, GetMinimumProviderConfidence(item));

    private bool IsBlockedByOutboundDataRestriction(ToolCatalogItem item) => !ToolSelectionRules.IsOutboundDataAllowed(this.RequiredOutboundDataRestriction.Restriction, item.Implementation);

    private string? GetProviderConfidenceHint(ToolCatalogItem item)
    {
        if (!this.IsBlockedByProviderConfidence(item))
            return null;

        return string.Format(
            this.T("This tool requires provider confidence {0}. The selected provider has {1}."),
            GetMinimumProviderConfidence(item).GetName(),
            this.ProviderConfidence.GetName());
    }

    /// <summary>
    /// Every reason why this tool is out of reach right now, as one text.
    /// </summary>
    /// <remarks>
    /// Several reasons can apply at once, such as a missing setting and a provider without the
    /// confidence the tool needs. One justified paragraph reads better than a stack of short lines.
    /// </remarks>
    private string GetWarningText(ToolCatalogItem item)
    {
        var warnings = new List<string>(4);
        if (!item.ConfigurationState.IsConfigured)
            warnings.Add(string.IsNullOrWhiteSpace(item.ConfigurationState.Message) ? T("Required settings are missing. Configure this tool before enabling it.") : item.ConfigurationState.Message);

        if (!item.IsActive)
            warnings.Add(T("This tool has been disabled by your organization."));

        var providerConfidenceHint = this.GetProviderConfidenceHint(item);
        if (!string.IsNullOrWhiteSpace(providerConfidenceHint))
            warnings.Add(providerConfidenceHint);

        if (this.IsBlockedByOutboundDataRestriction(item))
            warnings.Add(this.RequiredOutboundDataRestriction.GetToolBlockedMessage(this.SettingsManager.ConfigurationData.Mailboxes));

        return string.Join(' ', warnings);
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
