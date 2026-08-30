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
    /// The model continues a text instead of answering in a conversation.
    /// </summary>
    /// <remarks>
    /// These are the models from the era before chat completions, such as OpenAI's text-davinci-003.
    /// Some providers still offer them, but they only work through the completions endpoint. Asking
    /// them for a chat completion fails, so they must not show up as chat models.
    /// </remarks>
    TEXT_COMPLETION,

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
    /// The model generates or edits videos.
    /// </summary>
    VIDEO_GENERATION,

    /// <summary>
    /// The model transcribes audio into text.
    /// </summary>
    TRANSCRIPTION,

    /// <summary>
    /// The model speaks: it synthesizes speech from text, or answers in audio itself.
    /// </summary>
    /// <remarks>
    /// This covers the pure text-to-speech models as well as those which hold a conversation in
    /// audio, such as the audio models of OpenAI. The latter do accept text, but they are made for
    /// spoken input and output, so they do not belong among the chat models.
    /// </remarks>
    SPEECH_SYNTHESIS,

    /// <summary>
    /// The model holds a spoken conversation over a live connection.
    /// </summary>
    /// <remarks>
    /// These models expect a streaming connection of their own, usually a WebSocket, instead of the
    /// chat completion API. They cannot be used for a normal chat.
    /// </remarks>
    REALTIME,

    /// <summary>
    /// The model extracts text from images or scanned documents.
    /// </summary>
    OCR,

    /// <summary>
    /// The model classifies content for policy violations.
    /// </summary>
    MODERATION,

    /// <summary>
    /// Not a model at all.
    /// </summary>
    /// <remarks>
    /// Some providers list entries in their models endpoint which are no models, such as OpenAI's
    /// 'container' resource for its code interpreter. A provider talking to such an entry gets an
    /// error, so they must not appear in any of the model lists we show.
    /// </remarks>
    OTHER,
}