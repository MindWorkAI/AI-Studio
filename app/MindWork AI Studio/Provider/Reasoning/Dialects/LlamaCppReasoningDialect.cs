namespace AIStudio.Provider.Reasoning.Dialects;

/// <summary>
/// The reasoning mode and budget of the llama.cpp server.
/// </summary>
/// <remarks>
/// Its "reasoning" key is a mode rather than an object, and one of its three values means neither
/// yes nor no: "auto" hands the decision to the model's own template, which is exactly the case
/// where nobody has decided anything.
/// </remarks>
public sealed class LlamaCppReasoningDialect : IReasoningDialect
{
    /// <inheritdoc />
    public ReasoningDialect Dialect => ReasoningDialect.LLAMA_CPP;

    /// <inheritdoc />
    public ReasoningConfigurationState Detect(IDictionary<string, object> parameters)
    {
        var states = new List<ReasoningConfigurationState>();

        if (ReasoningParameters.TryGet(parameters, "reasoning", out var reasoning))
            states.Add(ModeOf(reasoning));

        if (ReasoningParameters.TryGet(parameters, "reasoning_budget", out var reasoningBudget))
            states.Add(ReasoningParameters.BudgetOf(reasoningBudget));

        if (ReasoningParameters.TryGet(parameters, "chat_template_kwargs", out var chatTemplateKwargs) &&
            chatTemplateKwargs is IDictionary<string, object> chatTemplateKwargsObject)
            states.Add(QwenThinkingDialect.In(chatTemplateKwargsObject));

        return ReasoningParameters.Merge(states);
    }

    /// <summary>
    /// Reads the reasoning mode.
    /// </summary>
    /// <param name="value">The configured mode.</param>
    /// <returns>What it says, which for "auto" is nothing.</returns>
    private static ReasoningConfigurationState ModeOf(object? value) => value switch
    {
        string text when text.Equals("on", StringComparison.OrdinalIgnoreCase) => ReasoningConfigurationState.EXPLICITLY_ENABLED,
        string text when text.Equals("off", StringComparison.OrdinalIgnoreCase) => ReasoningConfigurationState.EXPLICITLY_DISABLED,
        string text when text.Equals("auto", StringComparison.OrdinalIgnoreCase) => ReasoningConfigurationState.NOT_CONFIGURED,

        _ => ReasoningParameters.LevelOf(value),
    };
}