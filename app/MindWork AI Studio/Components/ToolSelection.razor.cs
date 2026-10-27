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
    private bool showUnavailableTools;
    private IReadOnlyList<ToolCatalogItem> catalog = [];

    protected override void OnParametersSet()
    {
        this.SelectedToolIds = this.ToolRegistry.NormalizeSelection(this.SelectedToolIds);
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
        {
            this.showUnavailableTools = false;
            this.catalog = await this.ToolRegistry.GetCatalogAsync(this.Component);
        }
    }

    private void Hide() => this.showSelection = false;

    private void ToggleUnavailableTools() => this.showUnavailableTools = !this.showUnavailableTools;

    /// <summary>
    /// Whether the selection keeps the tools which cannot be selected right now apart, behind a button.
    /// </summary>
    /// <remarks>
    /// Not while a response is running: no tool can be switched then, and the list shows the current
    /// selection as a whole, as the note above it says.
    /// </remarks>
    private bool KeepsUnavailableToolsApart => !this.Disabled;

    /// <summary>
    /// The tools the selection lists first: those the user can select right now, or all of them while a response is running.
    /// </summary>
    private List<ToolCatalogItem> GetItemsShownFirst() => this.KeepsUnavailableToolsApart
        ? [..this.catalog.Where(this.IsSelectable)]
        : [..this.catalog];

    /// <summary>
    /// The tools which cannot be selected right now, selected ones included, which the selection lists behind a button.
    /// </summary>
    private List<ToolCatalogItem> GetUnavailableItems() => this.KeepsUnavailableToolsApart
        ? [..this.catalog.Where(item => !this.IsSelectable(item))]
        : [];

    /// <summary>
    /// The text of the button which shows or hides the tools that cannot be selected right now.
    /// </summary>
    /// <remarks>
    /// A selected tool may end up behind the button, for example, when the user switches to a provider
    /// with less confidence. The text then says so, because the selection would otherwise seem to have
    /// lost it.
    /// </remarks>
    private string GetUnavailableToolsButtonText(IReadOnlyCollection<ToolCatalogItem> unavailableItems)
    {
        if (this.showUnavailableTools)
            return T("Hide unavailable tools");

        var selectedCount = unavailableItems.Count(item => this.SelectedToolIds.Contains(item.Id));
        if (unavailableItems.Count == 1)
            return selectedCount == 0 ? T("Show 1 unavailable tool") : T("Show 1 unavailable tool, which is selected");

        return selectedCount == 0
            ? string.Format(T("Show {0} unavailable tools"), unavailableItems.Count.CompactCount())
            : string.Format(T("Show {0} unavailable tools, {1} of them selected"), unavailableItems.Count.CompactCount(), selectedCount.CompactCount());
    }

    /// <summary>
    /// Whether this tool can be switched at all right now.
    /// </summary>
    /// <remarks>
    /// The switch and the row click share this, so both agree on when a tool is out of reach: the
    /// tool itself cannot be selected right now, see ToolSelectionRules.IsSelectable, a response is
    /// running, or the model cannot call tools in the first place.
    /// </remarks>
    private bool IsRowDisabled(ToolCatalogItem item) => !this.IsSelectable(item) || this.Disabled || !this.SupportsTools;

    private bool IsSelectable(ToolCatalogItem item) => ToolSelectionRules.IsSelectable(item, this.ProviderConfidence, this.RequiredOutboundDataRestriction.Restriction);

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

        await this.ChangeSelection(item.Id, !this.SelectedToolIds.Contains(item.Id));
    }

    private async Task ChangeSelection(string collectionId, bool isSelected)
    {
        if (isSelected && !this.ToolRegistry.IsToolActive(collectionId))
            return;

        var updated = new HashSet<string>(this.SelectedToolIds, StringComparer.Ordinal);
        if (isSelected)
            updated.Add(collectionId);
        else
            updated.Remove(collectionId);

        updated = this.ToolRegistry.NormalizeSelection(updated);
        this.SelectedToolIds = updated;
        await this.SelectedToolIdsChanged.InvokeAsync(updated);
    }

    // The catalog already carries the resolved level, so there is nothing to look up again:
    private static ConfidenceLevel GetMinimumProviderConfidence(ToolCatalogItem item) => item.MinimumProviderConfidence;

    private bool IsBlockedByProviderConfidence(ToolCatalogItem item) => !ToolSelectionRules.IsProviderConfidenceAllowed(this.ProviderConfidence, GetMinimumProviderConfidence(item));

    /// <summary>
    /// Whether the mailboxes the chat read from keep every tool of this entry back.
    /// </summary>
    private bool IsBlockedByOutboundDataRestriction(ToolCatalogItem item) => !ToolSelectionRules.IsOutboundDataAllowed(this.RequiredOutboundDataRestriction.Restriction, item);

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
            warnings.Add(this.RequiredOutboundDataRestriction.GetToolBlockedMessage(this.SettingsManager.ConfigurationData));

        return string.Join(' ', warnings);
    }

    private async Task OpenSettings(ToolCatalogItem item)
    {
        var parameters = new DialogParameters<ToolSettingsDialog>
        {
            { x => x.CollectionId, item.Id },
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
