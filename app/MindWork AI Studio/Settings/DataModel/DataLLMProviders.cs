using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataLLMProviders
{
    /// <summary>
    /// Should we show the provider confidence level?
    /// </summary>
    public bool ShowProviderConfidence { get; set; } = true;
    
    /// <summary>
    /// Which confidence scheme to use.
    /// </summary>
    public ConfidenceSchemes ConfidenceScheme { get; set; } = ConfidenceSchemes.TRUST_USA_EUROPE;

    /// <summary>
    /// Provide custom confidence levels for each LLM provider.
    /// </summary>
    public Dictionary<Providers, ConfidenceLevel> CustomConfidenceScheme { get; set; } = new();
}