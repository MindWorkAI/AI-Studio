namespace AIStudio.Provider.Reasoning.Dialects;

/// <summary>
/// The "enable_thinking" switch Qwen introduced and other servers took over.
/// </summary>
/// <remarks>
/// It is accepted at the top level and inside "chat_template_kwargs", because it is really an
/// argument to the chat template rather than to the API -- which is also why two other dialects ask
/// this one about their own kwargs object instead of repeating the two keys.
/// </remarks>
public sealed class QwenThinkingDialect : IReasoningDialect
{
    /// <inheritdoc />
    public ReasoningDialect Dialect => ReasoningDialect.QWEN_THINKING;

    /// <inheritdoc />
    public ReasoningConfigurationState Detect(IDictionary<string, object> parameters) => In(parameters);

    /// <summary>
    /// Reads the switch out of any parameter object, which need not be the top-level one.
    /// </summary>
    /// <param name="parameters">The object to look in.</param>
    /// <returns>What it says.</returns>
    public static ReasoningConfigurationState In(IDictionary<string, object> parameters)
    {
        var states = new List<ReasoningConfigurationState>();

        if (ReasoningParameters.TryGet(parameters, "enable_thinking", out var enableThinking))
            states.Add(ReasoningParameters.LevelOf(enableThinking));

        if (ReasoningParameters.TryGet(parameters, "chat_template_kwargs", out var chatTemplateKwargs) &&
            chatTemplateKwargs is IDictionary<string, object> chatTemplateKwargsObject &&
            ReasoningParameters.TryGet(chatTemplateKwargsObject, "enable_thinking", out var nestedEnableThinking))
            states.Add(ReasoningParameters.LevelOf(nestedEnableThinking));

        return ReasoningParameters.Merge(states);
    }
}