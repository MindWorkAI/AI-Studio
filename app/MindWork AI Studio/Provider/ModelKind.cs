namespace AIStudio.Provider;

/// <summary>
/// The kind of an AI model, i.e. what the model is made for.
/// </summary>
/// <remarks>
/// This describes what kind of model we are dealing with. It answers a different question than the
/// Capability enum: capabilities describe what a chat model is able to do, for example whether it
/// accepts images or performs reasoning. Note that Capability.EMBEDDING marks a chat model which is
/// able to create embeddings as well, whereas ModelKind.EMBEDDING marks a model whose only purpose
/// is creating embeddings.
/// </remarks>
public enum ModelKind
{
    /// <summary>
    /// The model is used for chat completions.
    /// </summary>
    /// <remarks>
    /// This is the fallback: we report a model as a chat model whenever we do not recognize any
    /// other kind. Providers keep adding models we have never heard of, and a model we fail to
    /// recognize must stay visible to the user instead of silently disappearing from their list.
    /// </remarks>
    CHAT,

    /// <summary>
    /// The model maps text or images into a vector space.
    /// </summary>
    EMBEDDING,

    /// <summary>
    /// The model scores documents against a query to reorder search results.
    /// </summary>
    RERANKING,

    /// <summary>
    /// The model generates or edits images.
    /// </summary>
    IMAGE_GENERATION,

    /// <summary>
    /// The model transcribes audio into text.
    /// </summary>
    TRANSCRIPTION,

    /// <summary>
    /// The model synthesizes speech from text.
    /// </summary>
    SPEECH_SYNTHESIS,

    /// <summary>
    /// The model extracts text from images or scanned documents.
    /// </summary>
    OCR,

    /// <summary>
    /// The model classifies content for policy violations.
    /// </summary>
    MODERATION,
}