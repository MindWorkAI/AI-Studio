using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataRetrievalContextValidation(Expression<Func<Data, DataRetrievalContextValidation>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataRetrievalContextValidation() : this(null)
    {
    }

    /// <summary>
    /// Enable the retrieval context validation?
    /// </summary>
    public bool EnableRetrievalContextValidation { get; set; } = ManagedConfiguration.Register(configSelection, n => n.EnableRetrievalContextValidation, false);
    
    /// <summary>
    /// Preselect any retrieval context validation options?
    /// </summary>
    public bool PreselectOptions { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectOptions, false);
    
    /// <summary>
    /// Preselect a retrieval context validation provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedProvider, string.Empty);

    /// <summary>
    /// The minimum confidence level of the provider which validates the retrieval contexts.
    /// </summary>
    /// <remarks>
    /// It applies only while PreselectOptions is on, and then to every provider which validates them, the fallbacks included.
    /// </remarks>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumProviderConfidence, ConfidenceLevel.NONE);

    /// <summary>
    /// Configure how many parallel validations to run.
    /// </summary>
    public int NumParallelValidations { get; set; } = ManagedConfiguration.Register(configSelection, n => n.NumParallelValidations, 3);
}