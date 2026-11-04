using System.Linq.Expressions;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Settings for indexing local data sources.
/// </summary>
/// <param name="configSelection">The managed-configuration selector.</param>
public sealed class DataDataSourceIndexing(Expression<Func<Data, DataDataSourceIndexing>>? configSelection = null)
{
    /// <summary>
    /// The longest pause between two indexing steps either bound may ask for.
    /// </summary>
    public const int MAX_PAUSE_BETWEEN_STEPS_SECONDS = 300;

    /// <summary>
    /// Initializes an unmanaged instance, e.g., for the JSON deserializer.
    /// </summary>
    public DataDataSourceIndexing() : this(null)
    {
    }

    /// <summary>
    /// Whether local data source embeddings should refresh automatically when files change.
    /// </summary>
    public bool AutomaticRefresh { get; set; } = true;

    /// <summary>
    /// The lower bound of the random pause between two indexing steps, in seconds.
    /// </summary>
    /// <remarks>
    /// The pause spares an embedding server which many people share, e.g., in an organization. Zero
    /// together with a zero upper bound means no pause.
    /// </remarks>
    public int MinimumPauseBetweenStepsSeconds { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumPauseBetweenStepsSeconds, 0);

    /// <summary>
    /// The upper bound of the random pause between two indexing steps, in seconds.
    /// </summary>
    public int MaximumPauseBetweenStepsSeconds { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MaximumPauseBetweenStepsSeconds, 0);
}
