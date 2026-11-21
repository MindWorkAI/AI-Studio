using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataTextContentCleaner(Expression<Func<Data, DataTextContentCleaner>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataTextContentCleaner() : this(null)
    {
    }

    /// <summary>
    /// Preselect any text content cleaner options?
    /// </summary>
    public bool PreselectAgentOptions { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectAgentOptions, false);
    
    /// <summary>
    /// Preselect a text content cleaner provider?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedAgentProvider, string.Empty);

    /// <summary>
    /// The minimum confidence level of the provider which cleans the text content.
    /// </summary>
    /// <remarks>
    /// It applies only while PreselectAgentOptions is on, and then to every provider which cleans it, the fallbacks included.
    /// </remarks>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumProviderConfidence, ConfidenceLevel.NONE);
}