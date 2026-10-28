using AIStudio.Provider;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Says when tools an assistant was told to use cannot reach the selected provider.
/// </summary>
/// <remarks>
/// Whoever named these tools — a document analysis policy, an assistant plugin — did so without
/// knowing which provider the user would pick. The user cannot switch a blocked tool on either,
/// because there is no selection to switch. Saying nothing would let the run quietly proceed
/// without them, which is why this belongs next to the provider selection: choosing another
/// provider is what resolves it.
/// </remarks>
public partial class ManagedToolsWarning : MSGComponentBase
{
    [Parameter]
    public AIStudio.Tools.Components Component { get; set; } = AIStudio.Tools.Components.CHAT;

    /// <summary>
    /// The tools of this run, as named by the assistant's own rules.
    /// </summary>
    [Parameter]
    public IReadOnlySet<string> ToolIds { get; set; } = new HashSet<string>();

    [Parameter]
    public AIStudio.Settings.Provider ProviderSettings { get; set; } = AIStudio.Settings.Provider.NONE;

    [Parameter]
    public string Class { get; set; } = "mb-3";

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    private IReadOnlyList<ToolCatalogItem> availableTools = [];
    private IReadOnlyList<ToolCatalogItem> toolsSwitchedOffByUser = [];

    /// <summary>
    /// Whether this run expects tools while the selected provider cannot call any.
    /// </summary>
    private bool NeedsToolCallingProvider => this.ToolIds.Count > 0 && this.SettingsManager.AreToolsEnabled() && !this.ProviderSettings.GetToolCallingAvailability().IsAvailable;

    /// <summary>
    /// The tools of this run whose settings are incomplete, so they cannot run at all.
    /// </summary>
    /// <remarks>
    /// Unlike the confidence case, no provider resolves this: the tool itself is missing something,
    /// such as the web search without a server address. Tools an organization switched off are left
    /// out, because completing their settings would not bring them back either.
    /// </remarks>
    private IReadOnlyList<string> ToolsNeedingConfiguration
    {
        get
        {
            if (this.ToolIds.Count is 0 || !this.SettingsManager.AreToolsEnabled())
                return [];

            var collectionIds = this.ToolRegistry.NormalizeSelection(this.ToolIds);
            return this.availableTools
                .Where(x => collectionIds.Contains(x.Id) && x.IsActive && !x.ConfigurationState.IsConfigured)
                .Select(x => x.DisplayName)
                .ToList();
        }
    }

    /// <summary>
    /// The tools of this run which the selected provider is not trusted enough to receive.
    /// </summary>
    /// <remarks>
    /// Tools switched off in the settings are not counted: choosing another provider would not
    /// bring them back, so naming them here would send the user after the wrong fix.
    /// </remarks>
    private IReadOnlyList<string> ToolsBeyondProviderConfidence
    {
        get
        {
            if (this.ToolIds.Count is 0 || !this.SettingsManager.AreToolsEnabled())
                return [];

            var providerConfidence = this.ProviderSettings == AIStudio.Settings.Provider.NONE
                ? ConfidenceLevel.NONE
                : this.ProviderSettings.UsedLLMProvider.GetConfidence(this.SettingsManager).Level;

            var collectionIds = this.ToolRegistry.NormalizeSelection(this.ToolIds);
            return this.availableTools
                .Where(x => collectionIds.Contains(x.Id) && x.IsActive)
                .Where(x => !ToolSelectionRules.IsProviderConfidenceAllowed(providerConfidence, x.MinimumProviderConfidence))
                .Select(x => x.DisplayName)
                .ToList();
        }
    }

    /// <summary>
    /// The tools of this run which the user switched off in the tool settings.
    /// </summary>
    /// <remarks>
    /// Whoever named these tools did not know that the user never needs them. The run goes on
    /// without them, as the user asked for, but saying nothing would leave them wondering why a
    /// policy does not search the web, say, long after they switched the web search off. Tools an
    /// organization switched off stay unnamed, as above.
    /// </remarks>
    private IReadOnlyList<string> ToolsSwitchedOffByUser
    {
        get
        {
            if (this.ToolIds.Count is 0 || !this.SettingsManager.AreToolsEnabled())
                return [];

            var collectionIds = this.ToolRegistry.NormalizeSelection(this.ToolIds);
            return this.toolsSwitchedOffByUser
                .Where(x => collectionIds.Contains(x.Id))
                .Select(x => x.DisplayName)
                .ToList();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await this.LoadToolsAsync();

        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Loads the tools of this component: those the user may use, and those they switched off.
    /// </summary>
    /// <remarks>
    /// The catalog of the component leaves out the tools the user switched off, so they come from
    /// all the tools this component knows.
    /// </remarks>
    private async Task LoadToolsAsync()
    {
        this.availableTools = await this.ToolRegistry.GetCatalogAsync(this.Component);
        this.toolsSwitchedOffByUser = (await this.ToolRegistry.GetCatalogAsync(this.ToolRegistry.GetDefinitionsForComponent(this.Component)))
            .Where(x => x.SwitchOffReason is ToolOfferBlockReason.TOOL_SWITCHED_OFF_BY_USER)
            .ToList();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                await this.LoadToolsAsync();
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }
}