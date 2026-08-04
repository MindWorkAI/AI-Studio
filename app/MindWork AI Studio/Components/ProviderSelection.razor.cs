using System.Diagnostics.CodeAnalysis;

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
    
    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
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
                
                // Get the minimum confidence level for this component, and/or the global minimum if enforced:
                var minimumLevel = this.SettingsManager.GetMinimumConfidenceLevel(component);
                
                // Override with the explicit minimum level if set and higher:
                if (this.ExplicitMinimumConfidence is not ConfidenceLevel.UNKNOWN && this.ExplicitMinimumConfidence > minimumLevel)
                    minimumLevel = this.ExplicitMinimumConfidence;
                
                // Filter providers based on the minimum confidence level:
                foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
                    if (provider.UsedLLMProvider != LLMProviders.NONE)
                        if (provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level >= minimumLevel)
                            yield return provider;
                break;
        }
    }

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            this.StateHasChanged();

        return Task.CompletedTask;
    }

    #endregion

    private readonly record struct CapabilityIcon(string Icon, string Tooltip);

    private readonly record struct ProviderSelectionItem(AIStudio.Settings.Provider Provider, IReadOnlyList<CapabilityIcon> CapabilityIcons);
}