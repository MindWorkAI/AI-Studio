namespace AIStudio.Provider.HuggingFace;

/// <summary>
/// Which of the Hugging Face endpoints a provider instance talks to.
/// </summary>
/// <remarks>
/// Hugging Face serves chatting and everything else from different places. Chat completions go to
/// the router's own OpenAI-compatible endpoint, which accepts the model IDs as the hub writes them
/// and picks an inference provider from a suffix. Embeddings do not exist there at all and have to
/// be asked of one provider's own route. Because the base URL is fixed when a provider instance is
/// built, the instance has to know from the start which of the two it is for.
/// </remarks>
public enum HFEndpointKind
{
    /// <summary>
    /// The router's own endpoint, which serves chat completions.
    /// </summary>
    CHAT,

    /// <summary>
    /// The OpenAI-compatible route of one inference provider, which serves embeddings.
    /// </summary>
    EMBEDDING,
}