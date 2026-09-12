namespace AIStudio.Provider.Reasoning.Dialects;

/// <summary>
/// The thinking token budget and chat template kwargs of vLLM.
/// </summary>
/// <remarks>
/// What vLLM accepts depends on the model family it was pointed at and on which reasoning parser
/// the operator started it with, so both the budget and the template arguments are read.
/// </remarks>
public sealed class VllmReasoningDialect : IReasoningDialect
{
    /// <inheritdoc />
    public ReasoningDialect Dialect => ReasoningDialect.VLLM;

    /// <inheritdoc />
    public ReasoningConfigurationState Detect(IDictionary<string, object> parameters)
    {
        var states = new List<ReasoningConfigurationState>();

        if (ReasoningParameters.TryGet(parameters, "thinking_token_budget", out var thinkingTokenBudget))
            states.Add(ReasoningParameters.BudgetOf(thinkingTokenBudget));

        if (ReasoningParameters.TryGet(parameters, "chat_template_kwargs", out var chatTemplateKwargs) &&
            chatTemplateKwargs is IDictionary<string, object> chatTemplateKwargsObject)
        {
            states.Add(QwenThinkingDialect.In(chatTemplateKwargsObject));

            if (ReasoningParameters.TryGet(chatTemplateKwargsObject, "thinking", out var thinking))
                states.Add(ReasoningParameters.LevelOf(thinking));
        }

        return ReasoningParameters.Merge(states);
    }
}