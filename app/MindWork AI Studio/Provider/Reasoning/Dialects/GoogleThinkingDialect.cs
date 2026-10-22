namespace AIStudio.Provider.Reasoning.Dialects;

/// <summary>
/// Google's thinking config, thinking level, and thought summaries.
/// </summary>
/// <remarks>
/// Google offers the same settings in several places at once: directly, under "generation_config",
/// and in both spellings of each key, because their own libraries write snake case while the REST
/// API answers in camel case. All of them are read, and the answers put together.
///
/// Summaries are the one setting which only ever says yes. Asking for thought summaries proves that
/// thinking is on; switching them off proves nothing, because a model can think without showing it.
/// </remarks>
public sealed class GoogleThinkingDialect : IReasoningDialect
{
    /// <inheritdoc />
    public ReasoningDialect Dialect => ReasoningDialect.GOOGLE_THINKING;

    /// <inheritdoc />
    public ReasoningConfigurationState Detect(IDictionary<string, object> parameters)
    {
        var states = new List<ReasoningConfigurationState>();

        if (ReasoningParameters.TryGet(parameters, "thinking_config", out var thinkingConfig) &&
            thinkingConfig is IDictionary<string, object> thinkingConfigObject)
            states.Add(ConfigOf(thinkingConfigObject));

        if (ReasoningParameters.TryGet(parameters, "generation_config", out var generationConfig) &&
            generationConfig is IDictionary<string, object> generationConfigObject)
        {
            if (ReasoningParameters.TryGet(generationConfigObject, "thinking_config", out var nestedThinkingConfig) &&
                nestedThinkingConfig is IDictionary<string, object> nestedThinkingConfigObject)
                states.Add(ConfigOf(nestedThinkingConfigObject));

            if (ReasoningParameters.TryGet(generationConfigObject, "thinking_summaries", out var thinkingSummaries))
                states.Add(SummariesOf(thinkingSummaries));

            if (ReasoningParameters.TryGet(generationConfigObject, "thinking_level", out var thinkingLevel))
                states.Add(ReasoningParameters.LevelOf(thinkingLevel));
        }

        if (ReasoningParameters.TryGet(parameters, "thinking_summaries", out var topLevelThinkingSummaries))
            states.Add(SummariesOf(topLevelThinkingSummaries));

        if (ReasoningParameters.TryGet(parameters, "thinking_level", out var topLevelThinkingLevel))
            states.Add(ReasoningParameters.LevelOf(topLevelThinkingLevel));

        return ReasoningParameters.Merge(states);
    }

    /// <summary>
    /// Reads a thinking config, in either spelling of its keys.
    /// </summary>
    /// <param name="thinkingConfig">The parsed thinking config object.</param>
    /// <returns>What it says.</returns>
    private static ReasoningConfigurationState ConfigOf(IDictionary<string, object> thinkingConfig)
    {
        var states = new List<ReasoningConfigurationState>();

        if (ReasoningParameters.TryGet(thinkingConfig, "thinking_budget", out var thinkingBudget) ||
            ReasoningParameters.TryGet(thinkingConfig, "thinkingBudget", out thinkingBudget))
            states.Add(ReasoningParameters.BudgetOf(thinkingBudget));

        if (ReasoningParameters.TryGet(thinkingConfig, "include_thoughts", out var includeThoughts) ||
            ReasoningParameters.TryGet(thinkingConfig, "includeThoughts", out includeThoughts))
            states.Add(ReasoningParameters.LevelOf(includeThoughts));

        return ReasoningParameters.Merge(states);
    }

    /// <summary>
    /// Reads a thought summary setting, which can only ever say yes.
    /// </summary>
    /// <param name="value">The configured summary setting.</param>
    /// <returns>Yes, when it asks for summaries; nothing otherwise.</returns>
    private static ReasoningConfigurationState SummariesOf(object? value) => value switch
    {
        string text when text.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
                         text.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                         text.Equals("summarized", StringComparison.OrdinalIgnoreCase)
            => ReasoningConfigurationState.EXPLICITLY_ENABLED,

        true => ReasoningConfigurationState.EXPLICITLY_ENABLED,

        _ => ReasoningConfigurationState.NOT_CONFIGURED,
    };
}