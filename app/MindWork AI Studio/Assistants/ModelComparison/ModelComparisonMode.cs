namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the Model Comparison assistant finds out about two models.
/// </summary>
public enum ModelComparisonMode
{
    /// <summary>
    /// How consistently the models answer one request, sent once or several times in a row.
    /// </summary>
    STABILITY,

    /// <summary>
    /// Which model helps the user more with real tasks, each worked on in a chat with both models, one after the other.
    /// </summary>
    TASKS,
}