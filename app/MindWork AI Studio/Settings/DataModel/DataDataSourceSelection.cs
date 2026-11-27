using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataDataSourceSelection(Expression<Func<Data, DataDataSourceSelection>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataDataSourceSelection() : this(null)
    {
    }

    /// <summary>
    /// Preselect any data source selection options?
    /// </summary>
    public bool PreselectOptions { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectOptions, false);
    
    /// <summary>
    /// Preselect a data source selection provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedProvider, string.Empty);

    /// <summary>
    /// The minimum confidence level of the provider which selects the data sources.
    /// </summary>
    /// <remarks>
    /// It applies only while PreselectOptions is on, and then to every provider which selects them, the fallbacks included.
    /// </remarks>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumProviderConfidence, ConfidenceLevel.NONE);
}