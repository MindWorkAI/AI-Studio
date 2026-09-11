namespace AIStudio.Models;

/// <summary>
/// Who built a model, as opposed to who serves it.
/// </summary>
/// <remarks>
/// The two are different questions, and mixing them up is what made the old rules delegate between
/// vendors until they called each other in circles. A provider is where a request goes; a vendor is
/// whose model answers it. Llama comes from Meta whether it arrives through Groq, Fireworks, or a
/// local Ollama.
///
/// A rule may bind itself to a vendor, which matters where the same name means two different models
/// depending on who made it. It is also what a gateway declares when it unwraps a name such as
/// "anthropic/claude-sonnet-4-0".
///
/// This list grows with the families being ported. Only vendors whose models the app already has
/// rules for are named here; adding a member is part of adding the family, not a step of its own.
/// </remarks>
public enum ModelVendor
{
    /// <summary>
    /// We do not know who built this model. This is the answer for everything not recognized.
    /// </summary>
    UNKNOWN,

    OPEN_AI,
    ANTHROPIC,
    GOOGLE,
    MISTRAL_AI,
    ALIBABA,
    DEEP_SEEK,
    PERPLEXITY,
    XAI,
    META,
    MICROSOFT,
    NVIDIA,
    IBM,
    COHERE,
    MOONSHOT_AI,
    TENCENT,
    Z_AI,
    MINIMAX,
    AI2,
    BYTE_DANCE,
    TII,
    INCLUSION_AI,
    BAIDU,
    HUGGING_FACE,
    SERVICE_NOW,
    SHANGHAI_AI_LAB,
    SWISS_AI,
}