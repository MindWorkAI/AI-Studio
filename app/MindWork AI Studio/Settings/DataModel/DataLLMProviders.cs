using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataLLMProviders
{
    /// <summary>
    /// Should we enforce a global minimum confidence level?
    /// </summary>
    public bool EnforceGlobalMinimumConfidence { get; set; }

    /// <summary>
    /// The global minimum confidence level to enforce.
    /// </summary>
    public ConfidenceLevel GlobalMinimumConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// Should we show the provider confidence level?
    /// </summary>
    public bool ShowProviderConfidence { get; set; } = true;
    
    /// <summary>
    /// Which confidence scheme to use.
    /// </summary>
    public ConfidenceSchemes ConfidenceScheme { get; set; } = ConfidenceSchemes.TRUST_ALL;

    /// <summary>
    /// Provide custom confidence levels for each LLM provider.
    /// </summary>
    public Dictionary<LLMProviders, ConfidenceLevel> CustomConfidenceScheme { get; set; } = new();
}