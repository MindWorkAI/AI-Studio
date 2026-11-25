namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Which results of the Model Comparison assistant the statistics count.
/// </summary>
public enum ModelComparisonResultSource
{
    /// <summary>
    /// The compared answers of the stability mode, and the blind tasks of every comparison series whose result the user
    /// saw.
    /// </summary>
    ALL,

    /// <summary>
    /// Only the compared answers of the stability mode.
    /// </summary>
    ANSWERS,

    /// <summary>
    /// Only the blind tasks of every comparison series whose result the user saw.
    /// </summary>
    TASKS,
}