namespace AIStudio.AIOperations.DataSourceChoice;

/// <summary>
/// The data sources a model chose for a prompt.
/// </summary>
/// <param name="SelectedDataSources">The chosen data sources, as the model named them; empty when it chose none.</param>
/// <param name="IsAnswered">Whether the model gave a usable answer. Without one, nothing was chosen either.</param>
public sealed record DataSourceChoiceResult(IReadOnlyList<SelectedDataSource> SelectedDataSources, bool IsAnswered)
{
    /// <summary>
    /// No usable answer: no provider, an empty prompt, or an answer without valid JSON.
    /// </summary>
    public static readonly DataSourceChoiceResult NOT_ANSWERED = new([], false);
}