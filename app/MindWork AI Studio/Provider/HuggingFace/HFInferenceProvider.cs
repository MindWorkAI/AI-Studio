namespace AIStudio.Provider.HuggingFace;

/// <summary>
/// Enum for inference providers that Hugging Face supports.
/// </summary>
/// <remarks>
/// Besides the providers themselves, this enum carries the routing strategies Hugging Face offers.
/// They are no providers, but they take the same place: the router picks a provider for us instead
/// of us naming one.
///
/// NONE must stay the first value: settings are read through the tolerant enum converter, which
/// falls back to the first value whenever it meets a name we no longer know. That is what happens
/// to a configuration naming one of the providers Hugging Face stopped routing in July 2026
/// (Hyperbolic, SambaNova, Nebius, NVIDIA, Clarifai, Black Forest Labs), and to one naming the
/// Hugging Face Inference API, which serves no model we can reach: it has no chat models at all,
/// and its OpenAI-compatible routes for embeddings and transcription do not exist. Such a provider
/// has to end up on NONE, where the validation asks the user to choose again. Were a routing
/// strategy first, those configurations would silently switch to automatic routing instead.
/// </remarks>
public enum HFInferenceProvider
{
    NONE,

    //
    // Routing strategies. Hugging Face writes them where a provider name would go:
    //
    AUTOMATIC,
    CHEAPEST,
    PREFERRED,

    //
    // The providers Hugging Face routes:
    //
    BASETEN,
    CEREBRAS,
    COHERE,
    DEEPINFRA,
    FEATHERLESS_AI,
    FIREWORKS,
    GROQ,
    NOVITA,
    NSCALE,
    OVHCLOUD,
    PUBLIC_AI,
    SCALEWAY,
    TOGETHER_AI,
    ZAI,
}