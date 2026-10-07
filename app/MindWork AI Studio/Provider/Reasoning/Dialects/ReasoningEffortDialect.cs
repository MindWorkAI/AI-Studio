namespace AIStudio.Provider.Reasoning.Dialects;

/// <summary>
/// The top-level "reasoning_effort" parameter.
/// </summary>
/// <remarks>
/// A dialect of its own although it is one key, because it travels on its own: providers accept it
/// without the nested object next to it, and the code this replaces had to remember to check for it
/// separately at every one of them. Here it is one line in the table instead.
/// </remarks>
public sealed class ReasoningEffortDialect : IReasoningDialect
{
    /// <inheritdoc />
    public ReasoningDialect Dialect => ReasoningDialect.REASONING_EFFORT;

    /// <inheritdoc />
    public ReasoningConfigurationState Detect(IDictionary<string, object> parameters) =>
        ReasoningParameters.TryGet(parameters, "reasoning_effort", out var effort)
            ? ReasoningParameters.LevelOf(effort)
            : ReasoningConfigurationState.NOT_CONFIGURED;
}