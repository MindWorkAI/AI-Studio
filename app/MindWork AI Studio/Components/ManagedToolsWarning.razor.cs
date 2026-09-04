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

    /// <summary>
    /// Whether this run expects tools while the selected provider cannot call any.
    /// </summary>
    private bool NeedsToolCallingProvider => this.ToolIds.Count > 0 && this.SettingsManager.AreToolsEnabled() && !this.ProviderSettings.GetToolCallingAvailability().IsAvailable;

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

            return this.availableTools
                .Where(x => this.ToolIds.Contains(x.Definition.Id) && x.IsActive)
                .Where(x => !ToolSelectionRules.IsProviderConfidenceAllowed(providerConfidence, x.MinimumProviderConfidence))
                .Select(x => x.Implementation.GetDisplayName())
                .ToList();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        this.availableTools = await this.ToolRegistry.GetCatalogAsync(this.Component);

        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                this.availableTools = await this.ToolRegistry.GetCatalogAsync(this.Component);
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }
}