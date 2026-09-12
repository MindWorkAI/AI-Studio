namespace AIStudio.Provider.Reasoning.Dialects;

/// <summary>
/// Ollama's "think" parameter.
/// </summary>
/// <remarks>
/// One key, and it takes a boolean as readily as a level, which is why it needs no reading of its
/// own beyond the ordinary one.
/// </remarks>
public sealed class OllamaThinkDialect : IReasoningDialect
{
    /// <inheritdoc />
    public ReasoningDialect Dialect => ReasoningDialect.OLLAMA_THINK;

    /// <inheritdoc />
    public ReasoningConfigurationState Detect(IDictionary<string, object> parameters) =>
        ReasoningParameters.TryGet(parameters, "think", out var think)
            ? ReasoningParameters.LevelOf(think)
            : ReasoningConfigurationState.NOT_CONFIGURED;
}