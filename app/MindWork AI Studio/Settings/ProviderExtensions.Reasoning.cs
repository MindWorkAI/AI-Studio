using AIStudio.Provider;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    /// <summary>
    /// The reasoning-related intent found in the configured additional API parameters.
    /// </summary>
    private enum ReasoningConfigurationState
    {
        /// <summary>
        /// No recognized reasoning parameter was found.
        /// </summary>
        NOT_CONFIGURED,

        /// <summary>
        /// A recognized reasoning parameter explicitly enables reasoning.
        /// </summary>
        EXPLICITLY_ENABLED,

        /// <summary>
        /// A recognized reasoning parameter explicitly disables reasoning.
        /// </summary>
        EXPLICITLY_DISABLED,
    }

    /// <summary>
    /// Get the effective reasoning indicator state for the configured provider instance.
    /// </summary>
    /// <param name="provider">The configured provider.</param>
    /// <returns>The effective reasoning indicator state.</returns>
    /// <remarks>
    /// This combines static model capabilities with per-provider additional API parameters.
    /// For default-on models, an explicit disabling parameter hides the icon; for optional
    /// models, an explicit enabling parameter is required before the icon is shown.
    /// </remarks>
    public static ReasoningIndicatorState GetReasoningIndicatorState(this Provider provider)
    {
        var capabilities = provider.GetModelCapabilities();
        if (capabilities.Contains(Capability.ALWAYS_REASONING))
            return ReasoningIndicatorState.ALWAYS_ON;
    
        var reasoningConfigurationState = GetReasoningConfigurationState(provider);
        if (capabilities.Contains(Capability.REASONING_BY_DEFAULT))
        {
            return reasoningConfigurationState switch
            {
                ReasoningConfigurationState.EXPLICITLY_DISABLED => ReasoningIndicatorState.NONE,
                ReasoningConfigurationState.EXPLICITLY_ENABLED => ReasoningIndicatorState.CONFIGURED,
                _ => ReasoningIndicatorState.DEFAULT_ON,
            };
        }

        if (capabilities.Contains(Capability.OPTIONAL_REASONING) &&
            reasoningConfigurationState is ReasoningConfigurationState.EXPLICITLY_ENABLED)
            return ReasoningIndicatorState.CONFIGURED;

        return ReasoningIndicatorState.NONE;
    }

    /// <summary>
    /// Parse additional API parameters and dispatch them to provider-specific reasoning detectors.
    /// </summary>
    /// <param name="provider">The configured provider whose additional API parameters should be inspected.</param>
    /// <returns>The explicit reasoning configuration state, or <see cref="ReasoningConfigurationState.NOT_CONFIGURED"/> if nothing known was found.</returns>
    private static ReasoningConfigurationState GetReasoningConfigurationState(Provider provider)
    {
        if (!AdditionalApiParametersParser.TryParse(provider.AdditionalJsonApiParameters, out var parameters, out _))
            return ReasoningConfigurationState.NOT_CONFIGURED;

        return provider.UsedLLMProvider switch
        {
            LLMProviders.OPEN_AI => MergeReasoningStates(
                GetOpenAICompatibleReasoningState(parameters),
                GetReasoningEffortState(parameters)),

            LLMProviders.ANTHROPIC => GetAnthropicReasoningState(parameters),

            LLMProviders.MISTRAL or LLMProviders.PERPLEXITY => GetReasoningEffortState(parameters),

            LLMProviders.GOOGLE => MergeReasoningStates(
                GetOpenAICompatibleReasoningState(parameters),
                GetGoogleReasoningState(parameters)),

            LLMProviders.ALIBABA_CLOUD => MergeReasoningStates(
                GetOpenAICompatibleReasoningState(parameters),
                GetQwenReasoningState(parameters)),

            LLMProviders.OPEN_ROUTER or
                LLMProviders.X or
                LLMProviders.DEEP_SEEK or
                LLMProviders.GROQ or
                LLMProviders.FIREWORKS or
                LLMProviders.HUGGINGFACE or
                LLMProviders.HELMHOLTZ or
                LLMProviders.GWDG => MergeReasoningStates(
                    GetOpenAICompatibleReasoningState(parameters),
                    GetReasoningEffortState(parameters),
                    GetQwenReasoningState(parameters),
                    GetGoogleReasoningState(parameters)),

            LLMProviders.SELF_HOSTED => provider.Host switch
            {
                Host.OLLAMA => MergeReasoningStates(
                    GetOpenAICompatibleReasoningState(parameters),
                    GetOllamaReasoningState(parameters),
                    GetQwenReasoningState(parameters)),

                Host.LLAMA_CPP => MergeReasoningStates(
                    GetOpenAICompatibleReasoningState(parameters),
                    GetLlamaCppReasoningState(parameters),
                    GetQwenReasoningState(parameters)),

                Host.VLLM => MergeReasoningStates(
                    GetOpenAICompatibleReasoningState(parameters),
                    GetReasoningEffortState(parameters),
                    GetVllmReasoningState(parameters),
                    GetQwenReasoningState(parameters),
                    GetGoogleReasoningState(parameters)),

                _ => MergeReasoningStates(
                    GetOpenAICompatibleReasoningState(parameters),
                    GetReasoningEffortState(parameters),
                    GetQwenReasoningState(parameters),
                    GetGoogleReasoningState(parameters)),
            },

            _ => ReasoningConfigurationState.NOT_CONFIGURED,
        };
    }

    /// <summary>
    /// Detect OpenAI-compatible reasoning parameters.
    /// </summary>
    /// <param name="parameters">The parsed additional API parameters.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    /// <remarks>
    /// OpenAI-compatible providers commonly use a nested <c>reasoning</c> object and/or
    /// a top-level <c>reasoning_effort</c> parameter.
    /// </remarks>
    private static ReasoningConfigurationState GetOpenAICompatibleReasoningState(IDictionary<string, object> parameters)
    {
        var reasoningState = ReasoningConfigurationState.NOT_CONFIGURED;
        if (TryGetParameter(parameters, "reasoning", out var reasoning))
        {
            reasoningState = reasoning switch
            {
                IDictionary<string, object> reasoningObject when TryGetParameter(reasoningObject, "effort", out var effort) => GetLevelState(effort),
                IDictionary<string, object> reasoningObject when TryGetParameter(reasoningObject, "summary", out var summary) => GetLevelState(summary),
                IDictionary<string, object> => ReasoningConfigurationState.NOT_CONFIGURED,
                _ => GetLevelState(reasoning),
            };
        }

        return MergeReasoningStates(reasoningState, GetReasoningEffortState(parameters));
    }

    /// <summary>
    /// Detect a top-level <c>reasoning_effort</c> parameter.
    /// </summary>
    /// <param name="parameters">The parsed additional API parameters.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    private static ReasoningConfigurationState GetReasoningEffortState(IDictionary<string, object> parameters)
    {
        return TryGetParameter(parameters, "reasoning_effort", out var reasoningEffort)
            ? GetLevelState(reasoningEffort)
            : ReasoningConfigurationState.NOT_CONFIGURED;
    }

    /// <summary>
    /// Detect Anthropic extended-thinking parameters.
    /// </summary>
    /// <param name="parameters">The parsed additional API parameters.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    private static ReasoningConfigurationState GetAnthropicReasoningState(IDictionary<string, object> parameters)
    {
        if (!TryGetParameter(parameters, "thinking", out var thinking))
            return ReasoningConfigurationState.NOT_CONFIGURED;

        return thinking switch
        {
            IDictionary<string, object> thinkingObject when TryGetParameter(thinkingObject, "type", out var type) => GetAnthropicThinkingTypeState(type),
            _ => GetLevelState(thinking),
        };
    }

    /// <summary>
    /// Detect Google Gemini thinking parameters across OpenAI-compatible additional parameters.
    /// </summary>
    /// <param name="parameters">The parsed additional API parameters.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    /// <remarks>
    /// Google can expose thinking options through <c>thinking_config</c>,
    /// <c>generation_config.thinking_config</c>, <c>thinking_level</c>, and summary settings.
    /// Summary settings only prove that thinking is enabled when they request summaries;
    /// disabling summaries does not necessarily disable reasoning.
    /// </remarks>
    private static ReasoningConfigurationState GetGoogleReasoningState(IDictionary<string, object> parameters)
    {
        var states = new List<ReasoningConfigurationState>();

        if (TryGetParameter(parameters, "thinking_config", out var thinkingConfig) &&
            thinkingConfig is IDictionary<string, object> thinkingConfigObject)
            states.Add(GetGoogleThinkingConfigState(thinkingConfigObject));

        if (TryGetParameter(parameters, "generation_config", out var generationConfig) &&
            generationConfig is IDictionary<string, object> generationConfigObject)
        {
            if (TryGetParameter(generationConfigObject, "thinking_config", out var nestedThinkingConfig) &&
                nestedThinkingConfig is IDictionary<string, object> nestedThinkingConfigObject)
                states.Add(GetGoogleThinkingConfigState(nestedThinkingConfigObject));

            if (TryGetParameter(generationConfigObject, "thinking_summaries", out var thinkingSummaries))
                states.Add(GetThinkingSummariesState(thinkingSummaries));

            if (TryGetParameter(generationConfigObject, "thinking_level", out var thinkingLevel))
                states.Add(GetLevelState(thinkingLevel));
        }

        if (TryGetParameter(parameters, "thinking_summaries", out var topLevelThinkingSummaries))
            states.Add(GetThinkingSummariesState(topLevelThinkingSummaries));

        if (TryGetParameter(parameters, "thinking_level", out var topLevelThinkingLevel))
            states.Add(GetLevelState(topLevelThinkingLevel));

        return MergeReasoningStates(states);
    }

    /// <summary>
    /// Detect Google Gemini thinking-budget and include-thoughts settings.
    /// </summary>
    /// <param name="thinkingConfig">The parsed <c>thinking_config</c> object.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    private static ReasoningConfigurationState GetGoogleThinkingConfigState(IDictionary<string, object> thinkingConfig)
    {
        var states = new List<ReasoningConfigurationState>();

        if (TryGetParameter(thinkingConfig, "thinking_budget", out var thinkingBudget) ||
            TryGetParameter(thinkingConfig, "thinkingBudget", out thinkingBudget))
            states.Add(GetBudgetState(thinkingBudget));

        if (TryGetParameter(thinkingConfig, "include_thoughts", out var includeThoughts) ||
            TryGetParameter(thinkingConfig, "includeThoughts", out includeThoughts))
            states.Add(GetLevelState(includeThoughts));

        return MergeReasoningStates(states);
    }

    /// <summary>
    /// Detect Google Gemini thinking-summary values that imply reasoning is active.
    /// </summary>
    /// <param name="value">The configured thinking-summary value.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    /// <remarks>
    /// A disabled or missing summary does not prove that thinking is disabled, so only
    /// known enabling values are treated as explicit reasoning configuration.
    /// </remarks>
    private static ReasoningConfigurationState GetThinkingSummariesState(object? value) => value switch
    {
        string text when text.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("on", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("summarized", StringComparison.OrdinalIgnoreCase)
            => ReasoningConfigurationState.EXPLICITLY_ENABLED,
    
        true => ReasoningConfigurationState.EXPLICITLY_ENABLED,
        _ => ReasoningConfigurationState.NOT_CONFIGURED,
    };

    /// <summary>
    /// Detect Ollama's <c>think</c> parameter.
    /// </summary>
    /// <param name="parameters">The parsed additional API parameters.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    private static ReasoningConfigurationState GetOllamaReasoningState(IDictionary<string, object> parameters)
    {
        return TryGetParameter(parameters, "think", out var think)
            ? GetLevelState(think)
            : ReasoningConfigurationState.NOT_CONFIGURED;
    }

    /// <summary>
    /// Detect llama.cpp server reasoning parameters.
    /// </summary>
    /// <param name="parameters">The parsed additional API parameters.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    /// <remarks>
    /// llama.cpp exposes runtime reasoning control through parameters such as
    /// <c>reasoning</c>, <c>reasoning_budget</c>, and template-specific kwargs.
    /// </remarks>
    private static ReasoningConfigurationState GetLlamaCppReasoningState(IDictionary<string, object> parameters)
    {
        var states = new List<ReasoningConfigurationState>();

        if (TryGetParameter(parameters, "reasoning", out var reasoning))
            states.Add(GetLlamaCppReasoningModeState(reasoning));

        if (TryGetParameter(parameters, "reasoning_budget", out var reasoningBudget))
            states.Add(GetBudgetState(reasoningBudget));

        if (TryGetParameter(parameters, "chat_template_kwargs", out var chatTemplateKwargs) &&
            chatTemplateKwargs is IDictionary<string, object> chatTemplateKwargsObject)
            states.Add(GetQwenReasoningState(chatTemplateKwargsObject));

        return MergeReasoningStates(states);
    }

    /// <summary>
    /// Detect vLLM reasoning parameters.
    /// </summary>
    /// <param name="parameters">The parsed additional API parameters.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    /// <remarks>
    /// vLLM supports both top-level reasoning fields and chat-template kwargs, depending
    /// on model family and reasoning parser configuration.
    /// </remarks>
    private static ReasoningConfigurationState GetVllmReasoningState(IDictionary<string, object> parameters)
    {
        var states = new List<ReasoningConfigurationState>();

        if (TryGetParameter(parameters, "thinking_token_budget", out var thinkingTokenBudget))
            states.Add(GetBudgetState(thinkingTokenBudget));

        if (TryGetParameter(parameters, "chat_template_kwargs", out var chatTemplateKwargs) &&
            chatTemplateKwargs is IDictionary<string, object> chatTemplateKwargsObject)
        {
            states.Add(GetQwenReasoningState(chatTemplateKwargsObject));

            if (TryGetParameter(chatTemplateKwargsObject, "thinking", out var thinking))
                states.Add(GetLevelState(thinking));
        }

        return MergeReasoningStates(states);
    }

    /// <summary>
    /// Detect Qwen-style <c>enable_thinking</c> parameters.
    /// </summary>
    /// <param name="parameters">The parsed additional API parameters.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    /// <remarks>
    /// Some OpenAI-compatible servers accept <c>enable_thinking</c> either at the
    /// top level or under <c>chat_template_kwargs</c>.
    /// </remarks>
    private static ReasoningConfigurationState GetQwenReasoningState(IDictionary<string, object> parameters)
    {
        var states = new List<ReasoningConfigurationState>();

        if (TryGetParameter(parameters, "enable_thinking", out var enableThinking))
            states.Add(GetLevelState(enableThinking));

        if (TryGetParameter(parameters, "chat_template_kwargs", out var chatTemplateKwargs) &&
            chatTemplateKwargs is IDictionary<string, object> chatTemplateKwargsObject &&
            TryGetParameter(chatTemplateKwargsObject, "enable_thinking", out var nestedEnableThinking))
            states.Add(GetLevelState(nestedEnableThinking));

        return MergeReasoningStates(states);
    }

    /// <summary>
    /// Interpret Anthropic's <c>thinking.type</c> value.
    /// </summary>
    /// <param name="value">The configured Anthropic thinking type.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    private static ReasoningConfigurationState GetAnthropicThinkingTypeState(object? value) => value switch
    {
        string text when text.Equals("enabled", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("adaptive", StringComparison.OrdinalIgnoreCase)
            => ReasoningConfigurationState.EXPLICITLY_ENABLED,
    
        string text when IsDisabledText(text) => ReasoningConfigurationState.EXPLICITLY_DISABLED,
        _ => GetLevelState(value),
    };

    /// <summary>
    /// Interpret llama.cpp's <c>reasoning</c> mode value.
    /// </summary>
    /// <param name="value">The configured llama.cpp reasoning mode.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    /// <remarks>
    /// <c>auto</c> means the server decides from the model/template, so it is treated as
    /// not configured by the user rather than as explicitly enabled.
    /// </remarks>
    private static ReasoningConfigurationState GetLlamaCppReasoningModeState(object? value) => value switch
    {
        string text when text.Equals("on", StringComparison.OrdinalIgnoreCase) => ReasoningConfigurationState.EXPLICITLY_ENABLED,
        string text when text.Equals("off", StringComparison.OrdinalIgnoreCase) => ReasoningConfigurationState.EXPLICITLY_DISABLED,
        string text when text.Equals("auto", StringComparison.OrdinalIgnoreCase) => ReasoningConfigurationState.NOT_CONFIGURED,
        _ => GetLevelState(value),
    };

    /// <summary>
    /// Interpret token-budget style values used by several providers.
    /// </summary>
    /// <param name="value">The configured budget value.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    /// <remarks>
    /// A zero budget disables reasoning; non-zero values, including unrestricted negative
    /// budgets, indicate that reasoning is available for the request.
    /// </remarks>
    private static ReasoningConfigurationState GetBudgetState(object? value) => value switch
    {
        int i => i is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        long l => l is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        double d => Math.Abs(d) < double.Epsilon ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        decimal m => m is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        _ => GetLevelState(value),
    };

    /// <summary>
    /// Interpret common boolean, numeric, and level-style reasoning values.
    /// </summary>
    /// <param name="value">The raw parsed parameter value.</param>
    /// <returns>The detected reasoning configuration state.</returns>
    private static ReasoningConfigurationState GetLevelState(object? value) => value switch
    {
        bool booleanValue => booleanValue ? ReasoningConfigurationState.EXPLICITLY_ENABLED : ReasoningConfigurationState.EXPLICITLY_DISABLED,
        int i => i is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        long l => l is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        double d => Math.Abs(d) < double.Epsilon ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        decimal m => m is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        string text when IsDisabledText(text) => ReasoningConfigurationState.EXPLICITLY_DISABLED,
        string text when IsEnabledText(text) => ReasoningConfigurationState.EXPLICITLY_ENABLED,
        _ => ReasoningConfigurationState.NOT_CONFIGURED,
    };

    /// <summary>
    /// Determine whether a string value is a known reasoning-enabling value.
    /// </summary>
    /// <param name="text">The string value to inspect.</param>
    /// <returns><see langword="true"/> if the value should be treated as enabling reasoning.</returns>
    private static bool IsEnabledText(string text)
    {
        return text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("enabled", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("low", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("minimal", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("medium", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("high", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("max", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determine whether a string value is a known reasoning-disabling value.
    /// </summary>
    /// <param name="text">The string value to inspect.</param>
    /// <returns><see langword="true"/> if the value should be treated as disabling reasoning.</returns>
    private static bool IsDisabledText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ||
               text.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("no", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("off", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("none", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("disabled", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Merge multiple detected reasoning states into a single state.
    /// </summary>
    /// <param name="states">The detected states from provider-specific parameter checks.</param>
    /// <returns>The merged state.</returns>
    /// <remarks>
    /// Explicit disabling wins over enabling because user-provided off switches should
    /// suppress default-on reasoning indicators.
    /// </remarks>
    private static ReasoningConfigurationState MergeReasoningStates(IEnumerable<ReasoningConfigurationState> states)
    {
        var result = ReasoningConfigurationState.NOT_CONFIGURED;
        foreach (var state in states)
        {
            if (state is ReasoningConfigurationState.EXPLICITLY_DISABLED)
                return ReasoningConfigurationState.EXPLICITLY_DISABLED;

            if (state is ReasoningConfigurationState.EXPLICITLY_ENABLED)
                result = ReasoningConfigurationState.EXPLICITLY_ENABLED;
        }

        return result;
    }

    /// <summary>
    /// Merge multiple detected reasoning states into a single state.
    /// </summary>
    /// <param name="states">The detected states from provider-specific parameter checks.</param>
    /// <returns>The merged state.</returns>
    private static ReasoningConfigurationState MergeReasoningStates(params ReasoningConfigurationState[] states)
    {
        return MergeReasoningStates(states.AsEnumerable());
    }

    /// <summary>
    /// Try to read a parameter from a dictionary using case-insensitive key matching.
    /// </summary>
    /// <param name="parameters">The parsed parameter dictionary.</param>
    /// <param name="key">The parameter name to find.</param>
    /// <param name="value">The matched parameter value, if found.</param>
    /// <returns><see langword="true"/> if a matching key was found; otherwise <see langword="false"/>.</returns>
    private static bool TryGetParameter(IDictionary<string, object> parameters, string key, out object? value)
    {
        value = null;
        if (parameters.Count is 0)
            return false;

        var foundKey = parameters.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        if (foundKey is null)
            return false;

        value = parameters[foundKey];
        return true;
    }
}