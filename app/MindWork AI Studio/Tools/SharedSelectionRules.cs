using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tools;

/// <summary>
/// Decides what a selection of tools or data sources offers when several models work with it, e.g., both models of a
/// comparison series of the Model Comparison assistant: each of them must be allowed everything the selection offers.
/// </summary>
/// <remarks>
/// With a single model, each rule gives what that model alone gives, so a selection for one model stays as it was.
/// </remarks>
public static class SharedSelectionRules
{
    /// <summary>
    /// Gets the confidence which counts for several models: the lowest of them, since a tool which one of them may not
    /// use must not be offered to any.
    /// </summary>
    /// <param name="levels">The confidence of each model.</param>
    /// <returns>The lowest confidence, or ConfidenceLevel.NONE without any model.</returns>
    public static ConfidenceLevel LowestConfidence(IEnumerable<ConfidenceLevel> levels) => levels.DefaultIfEmpty(ConfidenceLevel.NONE).Min();

    /// <summary>
    /// Tells whether several models can use tools: only when each of them can.
    /// </summary>
    /// <param name="availabilities">Whether each model can use tools.</param>
    /// <returns>The answer of the first model which cannot, with its reason, or that tools are available.</returns>
    public static ToolCallingAvailability ToolCallingOfAll(IEnumerable<ToolCallingAvailability> availabilities)
    {
        foreach (var availability in availabilities)
            if (!availability.IsAvailable)
                return availability;

        return ToolCallingAvailability.Available();
    }

    /// <summary>
    /// Decides how the data sources are searched when several models chat with the same data source options: by
    /// Semantic Search only when each of them can search itself, so that none gets passages the other has to look for.
    /// </summary>
    /// <param name="modes">How the data sources would be searched for each model on its own.</param>
    /// <returns>
    /// The mode of the first model which cannot search itself, with its reason; otherwise every message when the user
    /// prefers it, and Semantic Search when not. Without any model, every message, since nobody could search.
    /// </returns>
    public static EffectiveRetrievalMode RetrievalModeOfAll(IEnumerable<EffectiveRetrievalMode> modes)
    {
        EffectiveRetrievalMode? result = null;
        foreach (var mode in modes)
        {
            if (mode.SemanticSearchBlockReason is not ToolOfferBlockReason.NONE)
                return mode;

            if (result is null || mode.Mode is DataSourceRetrievalMode.EVERY_MESSAGE)
                result = mode;
        }

        return result ?? new(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.NONE);
    }

    /// <summary>
    /// Gets the data sources which several models may use: those which each of them may use.
    /// </summary>
    /// <remarks>
    /// Every list keeps only the data sources which the checks put into that list for each model, in the order of the
    /// first model. That holds for those waiting for a re-index or a repair as well: such a data source passed every
    /// check, and the reason belongs to the data source, so it lands in the same list for each model which may use it.
    /// </remarks>
    /// <param name="results">What the checks found for each model.</param>
    /// <returns>The data sources each model may use, and nothing without any model.</returns>
    public static AllowedSelectedDataSources DataSourcesOfAll(IReadOnlyList<AllowedSelectedDataSources> results)
    {
        if (results.Count == 0)
            return new([], [], [], [], []);

        return new(
            Shared(results, result => result.AllowedDataSources),
            Shared(results, result => result.SelectedDataSources),
            Shared(results, result => result.DataSourcesAwaitingReindex),
            Shared(results, result => result.DataSourcesNeedingRepair),
            Shared(results, result => result.UnreachableDataSources));
    }

    /// <summary>
    /// Gets the data sources of one list which every result holds in that list, in the order of the first result.
    /// </summary>
    private static IReadOnlyList<IDataSource> Shared(IReadOnlyList<AllowedSelectedDataSources> results, Func<AllowedSelectedDataSources, IReadOnlyList<IDataSource>> list)
    {
        var otherIds = results.Skip(1).Select(result => list(result).Select(dataSource => dataSource.Id).ToHashSet(StringComparer.Ordinal)).ToList();
        return [.. list(results[0]).Where(dataSource => otherIds.All(ids => ids.Contains(dataSource.Id)))];
    }
}