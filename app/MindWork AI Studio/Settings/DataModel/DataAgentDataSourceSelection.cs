using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataAgentDataSourceSelection(Expression<Func<Data, DataAgentDataSourceSelection>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataAgentDataSourceSelection() : this(null)
    {
    }

    /// <summary>
    /// Preselect any data source selection options?
    /// </summary>
    public bool PreselectAgentOptions { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectAgentOptions, false);
    
    /// <summary>
    /// Preselect a data source selection provider?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedAgentProvider, string.Empty);

    /// <summary>
    /// The minimum confidence level of the provider which selects the data sources.
    /// </summary>
    /// <remarks>
    /// It applies only while PreselectAgentOptions is on, and then to every provider which selects them, the fallbacks included.
    /// </remarks>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumProviderConfidence, ConfidenceLevel.NONE);
}