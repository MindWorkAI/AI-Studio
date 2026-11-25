namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Two models compared with each other, in the same order whichever of them was the first model of a comparison.
/// </summary>
/// <param name="First">The model which comes first by ModelComparisonModelKey.CompareTo.</param>
/// <param name="Second">The other model.</param>
public readonly record struct ModelComparisonPairKey(ModelComparisonModelKey First, ModelComparisonModelKey Second)
{
    /// <summary>
    /// Gets the pair of two models.
    /// </summary>
    /// <param name="one">One model.</param>
    /// <param name="other">The other model.</param>
    /// <returns>The pair, in its own order.</returns>
    public static ModelComparisonPairKey Of(ModelComparisonModelKey one, ModelComparisonModelKey other) => one.CompareTo(other) <= 0
        ? new(one, other)
        : new(other, one);
}