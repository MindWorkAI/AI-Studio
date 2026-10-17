namespace AIStudio.Assistants.ModelComparison;

public static class ModelComparisonPresentationOrderExtensions
{
    /// <summary>
    /// Gets the answer which is shown in column A.
    /// </summary>
    /// <param name="order">Which model is shown in column A.</param>
    /// <param name="run">The answers of both models.</param>
    /// <returns>The answer for column A.</returns>
    public static ModelComparisonAnswer InColumnA(this ModelComparisonPresentationOrder order, ModelComparisonRunResult run) => order switch
    {
        ModelComparisonPresentationOrder.FIRST_MODEL_FIRST => run.First,
        ModelComparisonPresentationOrder.SECOND_MODEL_FIRST => run.Second,

        _ => throw new ArgumentOutOfRangeException(nameof(order), order, $"The presentation order '{order}' is not known."),
    };

    /// <summary>
    /// Gets the answer which is shown in column B.
    /// </summary>
    /// <param name="order">Which model is shown in column A.</param>
    /// <param name="run">The answers of both models.</param>
    /// <returns>The answer for column B.</returns>
    public static ModelComparisonAnswer InColumnB(this ModelComparisonPresentationOrder order, ModelComparisonRunResult run) => order switch
    {
        ModelComparisonPresentationOrder.FIRST_MODEL_FIRST => run.Second,
        ModelComparisonPresentationOrder.SECOND_MODEL_FIRST => run.First,

        _ => throw new ArgumentOutOfRangeException(nameof(order), order, $"The presentation order '{order}' is not known."),
    };

    /// <summary>
    /// Turns a column-based choice back into the model it means. The reverse of
    /// <see cref="InColumnA"/>/<see cref="InColumnB"/>: those turn a known model into its column,
    /// this turns a known column back into its model.
    /// </summary>
    /// <param name="order">Which model stood in column A for this choice.</param>
    /// <param name="vote">The column-based choice, from the user's own vote or from a judge verdict.</param>
    public static ModelComparisonModelChoice ToModelChoice(this ModelComparisonPresentationOrder order, ModelComparisonVote vote) => vote switch
    {
        ModelComparisonVote.TIE => ModelComparisonModelChoice.TIE,
        ModelComparisonVote.COLUMN_A => order is ModelComparisonPresentationOrder.FIRST_MODEL_FIRST ? ModelComparisonModelChoice.FIRST : ModelComparisonModelChoice.SECOND,
        ModelComparisonVote.COLUMN_B => order is ModelComparisonPresentationOrder.FIRST_MODEL_FIRST ? ModelComparisonModelChoice.SECOND : ModelComparisonModelChoice.FIRST,

        _ => throw new ArgumentOutOfRangeException(nameof(vote), vote, $"The vote '{vote}' is not known."),
    };
}