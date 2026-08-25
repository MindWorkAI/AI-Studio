using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProviderSelection : MSGComponentBase
{
    [CascadingParameter]
    public Tools.Components? Component { get; set; }

    [Parameter]
    public AIStudio.Settings.Provider ProviderSettings { get; set; } = AIStudio.Settings.Provider.NONE;
    
    [Parameter]
    public EventCallback<AIStudio.Settings.Provider> ProviderSettingsChanged { get; set; }
    
    [Parameter]
    public Func<AIStudio.Settings.Provider, string?> ValidateProvider { get; set; } = _ => null;

    /// <summary>
    /// Gets or sets whether provider selection is disabled.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public ConfidenceLevel ExplicitMinimumConfidence { get; set; } = ConfidenceLevel.UNKNOWN;
    
    [Inject]
    private ILogger<ProviderSelection> Logger { get; init; } = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    #endregion
    
    private async Task SelectionChanged(AIStudio.Settings.Provider provider)
    {
        this.ProviderSettings = provider;
        await this.ProviderSettingsChanged.InvokeAsync(provider);
    }

    private IEnumerable<ProviderSelectionItem> GetAvailableProviderSelectionItems()
    {
        foreach (var provider in this.GetAvailableProviders())
            yield return new(provider, this.GetCapabilityIcons(provider));
    }

    private IReadOnlyList<CapabilityIcon> GetCapabilityIcons(AIStudio.Settings.Provider provider)
    {
        var capabilities = provider.GetModelCapabilities();
        List<CapabilityIcon> capabilityIcons = [];

        if (capabilities.Contains(Capability.AUDIO_INPUT))
            capabilityIcons.Add(new(Icons.Material.Filled.GraphicEq, this.T("Audio input possible")));

        if (capabilities.Contains(Capability.SINGLE_IMAGE_INPUT) || capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT))
            capabilityIcons.Add(new(Icons.Material.Filled.Image, this.T("Image input possible")));

        if (capabilities.Contains(Capability.SPEECH_INPUT))
            capabilityIcons.Add(new(Icons.Material.Filled.Mic, this.T("Speech input possible")));

        var reasoningIndicatorState = provider.GetReasoningIndicatorState();
        if (reasoningIndicatorState is not ReasoningIndicatorState.NONE)
            capabilityIcons.Add(new(Icons.Material.Filled.Psychology, this.GetReasoningTooltip(reasoningIndicatorState)));

        return capabilityIcons;
    }

    private string GetReasoningTooltip(ReasoningIndicatorState reasoningIndicatorState) => reasoningIndicatorState switch
    {
        ReasoningIndicatorState.DEFAULT_ON => this.T("Uses reasoning (thinking) by default"),
        ReasoningIndicatorState.CONFIGURED => this.T("Uses reasoning (thinking) configured by settings"),
        _ => this.T("Uses reasoning (thinking)"),
    };
    
    private IEnumerable<AIStudio.Settings.Provider> GetAvailableProviders()
    {
        switch (this.Component)
        {
            case null:
                this.Logger.LogError("Component is null! Cannot filter providers based on component settings. Missed CascadingParameter?");
                yield break;

            case Tools.Components.NONE:
                this.Logger.LogError("Component is NONE! Cannot filter providers based on component settings. Used wrong component?");
                yield break;

            case { } component:

                // Filter providers based on the minimum confidence level of this component, the
                // enforced global minimum, and the explicit minimum level when it is higher:
                foreach (var provider in this.SettingsManager.GetConfidentProviders(component, this.ExplicitMinimumConfidence))
                    yield return provider;
                break;
        }
    }

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
        {
            //
            // We hold a copy of the provider record, which is a snapshot taken when it was selected.
            // Once the user edits that provider, our copy is stale and would keep showing the old
            // name and the old icon, so we resolve it again and hand the fresh one to our parent:
            //
            var updatedProvider = this.SettingsManager.GetProviderById(this.ProviderSettings.Id);
            if (updatedProvider != AIStudio.Settings.Provider.NONE && updatedProvider != this.ProviderSettings)
            {
                this.ProviderSettings = updatedProvider;
                await this.ProviderSettingsChanged.InvokeAsync(updatedProvider);
            }

            this.StateHasChanged();
        }
    }

    #endregion

    private readonly record struct CapabilityIcon(string Icon, string Tooltip);

    private readonly record struct ProviderSelectionItem(AIStudio.Settings.Provider Provider, IReadOnlyList<CapabilityIcon> CapabilityIcons);
}
