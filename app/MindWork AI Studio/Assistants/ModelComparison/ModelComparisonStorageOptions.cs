namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Overrides where the Model Comparison assistant stores its files, for tests.
/// </summary>
public sealed class ModelComparisonStorageOptions
{
    /// <summary>
    /// Gets or initializes the folder in which the folder modelComparisons is created.
    /// </summary>
    public string? DataDirectory { get; init; }
}