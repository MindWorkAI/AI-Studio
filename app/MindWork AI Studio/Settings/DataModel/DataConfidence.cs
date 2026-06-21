using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataConfidence(Expression<Func<Data, DataConfidence>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataConfidence() : this(null)
    {
    }

    /// <summary>
    /// Should we enforce a global minimum confidence level?
    /// </summary>
    public bool EnforceGlobalMinimumConfidence { get; set; } = ManagedConfiguration.Register(configSelection, n => n.EnforceGlobalMinimumConfidence, false);

    /// <summary>
    /// The global minimum confidence level to enforce.
    /// </summary>
    public ConfidenceLevel GlobalMinimumConfidence { get; set; } = ManagedConfiguration.Register(configSelection, n => n.GlobalMinimumConfidence, ConfidenceLevel.NONE);
    
    /// <summary>
    /// Should we show the provider confidence level?
    /// </summary>
    public bool ShowProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowProviderConfidence, true);
    
    /// <summary>
    /// Which confidence scheme to use.
    /// </summary>
    public ConfidenceSchemes ConfidenceScheme { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ConfidenceScheme, ConfidenceSchemes.TRUST_ALL);

    /// <summary>
    /// Provide custom confidence levels for each provider family.
    /// </summary>
    public Dictionary<LLMProviders, ConfidenceLevel> CustomConfidenceScheme { get; set; } = ManagedConfiguration.Register(configSelection, n => n.CustomConfidenceScheme, []);
}