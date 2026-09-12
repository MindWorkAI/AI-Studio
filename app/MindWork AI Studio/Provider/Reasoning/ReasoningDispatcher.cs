using System.Collections.Concurrent;
using System.Collections.Frozen;

using AIStudio.Provider.Reasoning.Dialects;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Provider.Reasoning;

/// <summary>
/// Decides which dialects a provider speaks, and reads its parameters in all of them.
/// </summary>
/// <remarks>
/// Which dialect answers for which provider is a table here rather than a chain of checks spread
/// through the reading itself. That is the whole point of the split: adding a provider means adding
/// a line, and reading what one accepts means reading one line.
///
/// The answer is worked out once per provider setting. The question is asked from the provider list,
/// which re-renders whenever anything on the page changes, and the old code parsed the JSON a person
/// typed into their expert settings on every one of those renders. Nothing here reaches for
/// application state, so a test can ask it without the app having started.
/// </remarks>
public static class ReasoningDispatcher
{
    /// <summary>
    /// Every dialect there is, in the order the enum names them.
    /// </summary>
    private static readonly FrozenDictionary<ReasoningDialect, IReasoningDialect> DIALECTS = new IReasoningDialect[]
    {
        new OpenAICompatibleDialect(),
        new ReasoningEffortDialect(),
        new AnthropicThinkingDialect(),
        new GoogleThinkingDialect(),
        new QwenThinkingDialect(),
        new OllamaThinkDialect(),
        new LlamaCppReasoningDialect(),
        new VllmReasoningDialect(),
    }.ToFrozenDictionary(dialect => dialect.Dialect);

    /// <summary>
    /// Every dialect there is, in the order the enum names them.
    /// </summary>
    public static IReadOnlyList<IReasoningDialect> Dialects { get; } = DIALECTS.Values.OrderBy(dialect => dialect.Dialect).ToList();

    /// <summary>
    /// What an OpenAI-compatible server understands when nothing more is known about it.
    /// </summary>
    /// <remarks>
    /// The gateways and resellers serve everybody's models, so they are asked in every dialect a
    /// model of any vendor might answer to. Reading one dialect too many costs a dictionary lookup;
    /// reading one too few hides a switch the person has set.
    /// </remarks>
    private static readonly ReasoningDialect[] EVERYTHING_A_GATEWAY_MIGHT_SERVE =
    [
        ReasoningDialect.OPEN_AI_COMPATIBLE,
        ReasoningDialect.REASONING_EFFORT,
        ReasoningDialect.QWEN_THINKING,
        ReasoningDialect.GOOGLE_THINKING,
    ];

    private static readonly ReasoningDialect[] NOTHING = [];

    /// <summary>
    /// The answers already worked out, so that the same settings are read once.
    /// </summary>
    private static readonly ConcurrentDictionary<(LLMProviders Provider, Host Host, string Parameters), ReasoningConfigurationState> ANSWERED = new();

    /// <summary>
    /// Reads what a provider's additional API parameters say about reasoning.
    /// </summary>
    /// <param name="provider">The LLM provider.</param>
    /// <param name="host">The engine behind it, which only matters for self-hosted providers.</param>
    /// <param name="additionalParameters">The additional API parameters, as the person wrote them.</param>
    /// <returns>What they say, which is usually nothing.</returns>
    public static ReasoningConfigurationState WhatTheParametersSay(LLMProviders provider, Host host, string? additionalParameters)
    {
        if (string.IsNullOrWhiteSpace(additionalParameters))
            return ReasoningConfigurationState.NOT_CONFIGURED;

        return ANSWERED.GetOrAdd((provider, host, additionalParameters), static key => Read(key.Provider, key.Host, key.Parameters));
    }

    /// <summary>
    /// Which dialects this provider speaks.
    /// </summary>
    /// <remarks>
    /// The commercial providers are asked only in their own dialect plus whatever their API
    /// documents, because a parameter they do not accept says nothing about what they will do. The
    /// self-hosted engines are the other case: the operator picked the engine, so what it accepts is
    /// known, and it is the engine rather than the model which decides.
    /// </remarks>
    /// <param name="provider">The LLM provider.</param>
    /// <param name="host">The engine behind it.</param>
    /// <returns>The dialects to read the parameters in.</returns>
    public static IReadOnlyList<ReasoningDialect> DialectsOf(LLMProviders provider, Host host) => provider switch
    {
        LLMProviders.OPEN_AI => [ReasoningDialect.OPEN_AI_COMPATIBLE, ReasoningDialect.REASONING_EFFORT],

        LLMProviders.ANTHROPIC => [ReasoningDialect.ANTHROPIC_THINKING],

        LLMProviders.MISTRAL or LLMProviders.PERPLEXITY => [ReasoningDialect.REASONING_EFFORT],

        LLMProviders.GOOGLE => [ReasoningDialect.OPEN_AI_COMPATIBLE, ReasoningDialect.REASONING_EFFORT, ReasoningDialect.GOOGLE_THINKING],

        LLMProviders.ALIBABA_CLOUD => [ReasoningDialect.OPEN_AI_COMPATIBLE, ReasoningDialect.REASONING_EFFORT, ReasoningDialect.QWEN_THINKING],

        LLMProviders.OPEN_ROUTER or
            LLMProviders.HETZNER or
            LLMProviders.IONOS or
            LLMProviders.LITE_LLM or
            LLMProviders.X or
            LLMProviders.DEEP_SEEK or
            LLMProviders.GROQ or
            LLMProviders.FIREWORKS or
            LLMProviders.HUGGINGFACE or
            LLMProviders.HELMHOLTZ or
            LLMProviders.GWDG => EVERYTHING_A_GATEWAY_MIGHT_SERVE,

        LLMProviders.SELF_HOSTED => host switch
        {
            Host.OLLAMA => [ReasoningDialect.OPEN_AI_COMPATIBLE, ReasoningDialect.REASONING_EFFORT, ReasoningDialect.QWEN_THINKING, ReasoningDialect.OLLAMA_THINK],

            Host.LLAMA_CPP => [ReasoningDialect.OPEN_AI_COMPATIBLE, ReasoningDialect.REASONING_EFFORT, ReasoningDialect.QWEN_THINKING, ReasoningDialect.LLAMA_CPP],

            Host.VLLM => [ReasoningDialect.OPEN_AI_COMPATIBLE, ReasoningDialect.REASONING_EFFORT, ReasoningDialect.QWEN_THINKING, ReasoningDialect.GOOGLE_THINKING, ReasoningDialect.VLLM],

            _ => EVERYTHING_A_GATEWAY_MIGHT_SERVE,
        },

        _ => NOTHING,
    };

    /// <summary>
    /// Parses the parameters and asks every dialect this provider speaks.
    /// </summary>
    /// <param name="provider">The LLM provider.</param>
    /// <param name="host">The engine behind it.</param>
    /// <param name="additionalParameters">The additional API parameters.</param>
    /// <returns>What they say.</returns>
    private static ReasoningConfigurationState Read(LLMProviders provider, Host host, string additionalParameters)
    {
        if (!AdditionalApiParametersParser.TryParse(additionalParameters, out var parameters, out _))
            return ReasoningConfigurationState.NOT_CONFIGURED;

        return ReasoningParameters.Merge(DialectsOf(provider, host).Select(key => DIALECTS[key].Detect(parameters)));
    }
}