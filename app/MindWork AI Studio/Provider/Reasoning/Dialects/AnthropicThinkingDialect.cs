namespace AIStudio.Provider.Reasoning.Dialects;

/// <summary>
/// Anthropic's extended thinking, written as a "thinking" object.
/// </summary>
/// <remarks>
/// The object carries a type, and the two types which switch thinking on are named outright:
/// "enabled" and "adaptive". Everything else falls through to the ordinary reading of a value, so
/// that a person writing "thinking": false is understood as well.
/// </remarks>
public sealed class AnthropicThinkingDialect : IReasoningDialect
{
    /// <inheritdoc />
    public ReasoningDialect Dialect => ReasoningDialect.ANTHROPIC_THINKING;

    /// <inheritdoc />
    public ReasoningConfigurationState Detect(IDictionary<string, object> parameters)
    {
        if (!ReasoningParameters.TryGet(parameters, "thinking", out var thinking))
            return ReasoningConfigurationState.NOT_CONFIGURED;

        return thinking switch
        {
            IDictionary<string, object> thinkingObject when ReasoningParameters.TryGet(thinkingObject, "type", out var type) => TypeOf(type),

            _ => ReasoningParameters.LevelOf(thinking),
        };
    }

    /// <summary>
    /// Reads the "type" of an Anthropic thinking object.
    /// </summary>
    /// <param name="value">The configured thinking type.</param>
    /// <returns>What it says.</returns>
    private static ReasoningConfigurationState TypeOf(object? value) => value switch
    {
        string text when text.Equals("enabled", StringComparison.OrdinalIgnoreCase) ||
                         text.Equals("adaptive", StringComparison.OrdinalIgnoreCase)
            => ReasoningConfigurationState.EXPLICITLY_ENABLED,

        string text when ReasoningParameters.IsDisabledText(text) => ReasoningConfigurationState.EXPLICITLY_DISABLED,

        _ => ReasoningParameters.LevelOf(value),
    };
}