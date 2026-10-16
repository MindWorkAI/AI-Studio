namespace AIStudio.Provider.Reasoning;

/// <summary>
/// The ways a request can be asked to think, one per way of writing it down.
/// </summary>
/// <remarks>
/// Every provider speaks one or more of these, and which ones is stated in the dispatcher rather
/// than worked out from anything. The order here is the order they are asked in: the answer does not
/// depend on it -- a "no" wins wherever it stands -- but a report which named them in whatever order
/// a container handed them over would read differently on another machine.
/// </remarks>
public enum ReasoningDialect
{
    /// <summary>
    /// The nested "reasoning" object most OpenAI-compatible servers accept.
    /// </summary>
    OPEN_AI_COMPATIBLE,

    /// <summary>
    /// The top-level "reasoning_effort" parameter.
    /// </summary>
    REASONING_EFFORT,

    /// <summary>
    /// Anthropic's extended thinking, written as a "thinking" object.
    /// </summary>
    ANTHROPIC_THINKING,

    /// <summary>
    /// Google's thinking config, thinking level, and thought summaries.
    /// </summary>
    GOOGLE_THINKING,

    /// <summary>
    /// The "enable_thinking" switch Qwen introduced and other servers took over.
    /// </summary>
    QWEN_THINKING,

    /// <summary>
    /// Ollama's "think" parameter.
    /// </summary>
    OLLAMA_THINK,

    /// <summary>
    /// The reasoning mode and budget of the llama.cpp server.
    /// </summary>
    LLAMA_CPP,

    /// <summary>
    /// The thinking token budget and chat template kwargs of vLLM.
    /// </summary>
    VLLM,
}