namespace AIStudio.Provider.Reasoning.Dialects;

/// <summary>
/// The nested "reasoning" object almost every OpenAI-compatible server accepts.
/// </summary>
/// <remarks>
/// The object may carry an effort or a summary setting, and it may be written as a plain value
/// instead. An object carrying neither says nothing: somebody who wrote "reasoning": {} has not
/// asked for anything yet.
/// </remarks>
public sealed class OpenAICompatibleDialect : IReasoningDialect
{
    /// <inheritdoc />
    public ReasoningDialect Dialect => ReasoningDialect.OPEN_AI_COMPATIBLE;

    /// <inheritdoc />
    public ReasoningConfigurationState Detect(IDictionary<string, object> parameters)
    {
        if (!ReasoningParameters.TryGet(parameters, "reasoning", out var reasoning))
            return ReasoningConfigurationState.NOT_CONFIGURED;

        return reasoning switch
        {
            IDictionary<string, object> reasoningObject when ReasoningParameters.TryGet(reasoningObject, "effort", out var effort) => ReasoningParameters.LevelOf(effort),
            IDictionary<string, object> reasoningObject when ReasoningParameters.TryGet(reasoningObject, "summary", out var summary) => ReasoningParameters.LevelOf(summary),
            IDictionary<string, object> => ReasoningConfigurationState.NOT_CONFIGURED,

            _ => ReasoningParameters.LevelOf(reasoning),
        };
    }
}