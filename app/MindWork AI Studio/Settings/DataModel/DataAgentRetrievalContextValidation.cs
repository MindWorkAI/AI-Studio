using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataAgentRetrievalContextValidation(Expression<Func<Data, DataAgentRetrievalContextValidation>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataAgentRetrievalContextValidation() : this(null)
    {
    }

    /// <summary>
    /// Enable the retrieval context validation agent?
    /// </summary>
    public bool EnableRetrievalContextValidation { get; set; } = ManagedConfiguration.Register(configSelection, n => n.EnableRetrievalContextValidation, false);
    
    /// <summary>
    /// Preselect any retrieval context validation options?
    /// </summary>
    public bool PreselectAgentOptions { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectAgentOptions, false);
    
    /// <summary>
    /// Preselect a retrieval context validation provider?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedAgentProvider, string.Empty);

    /// <summary>
    /// The minimum confidence level of the provider which validates the retrieval contexts.
    /// </summary>
    /// <remarks>
    /// It applies only while PreselectAgentOptions is on, and then to every provider which validates them, the fallbacks included.
    /// </remarks>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumProviderConfidence, ConfidenceLevel.NONE);

    /// <summary>
    /// Configure how many parallel validations to run.
    /// </summary>
    public int NumParallelValidations { get; set; } = ManagedConfiguration.Register(configSelection, n => n.NumParallelValidations, 3);
}