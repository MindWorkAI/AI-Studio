namespace AIStudio.Provider;

/// <summary>
/// Determines what kind of model we are dealing with, based on its name.
/// </summary>
/// <remarks>
/// Many providers serve every kind of model through one models endpoint, without telling us what
/// kind each model is. Before this class existed, every provider carried its own list of name
/// fragments to sort those models apart. Those lists disagreed with each other: a model like
/// nomic-embed-text was recognized as an embedding model by some providers, while others offered it
/// as a chat model. The knowledge about model families is the same for all providers, so it lives
/// here now.
///
/// This class recognizes what a model is NOT made for. Everything we do not recognize is reported as
/// a chat model. That direction matters: when a provider adds a model family we have never seen, the
/// user still gets to use it. Getting it wrong the other way around would hide a model the user is
/// paying for.
///
/// What this class must not become is a place for provider-specific knowledge. That a model called
/// "codestral" is a fill-in-the-middle model at Mistral, or that Alibaba's chat models all start
/// with a "q", is true for that one provider only. Such rules stay in the provider.
/// </remarks>
public static class ModelKindExtensions
{
    //
    // Checked first, because these entries are no models at all: whatever else their name might
    // suggest, none of the other kinds applies to them.
    //
    private static readonly string[] OTHER_MARKERS = ["container"];

    //
    // Reranking is checked before embedding: rerankers are commonly named after the embedding model
    // they belong to, e.g. Qwen3-VL-Reranker-8B next to Qwen3-VL-Embedding-8B.
    //
    private static readonly string[] RERANKING_MARKERS = ["rerank"];

    private static readonly string[] EMBEDDING_MARKERS = ["embed", "bge", "mpnet", "paraphrase", "sentence-transformers", "gte-", "e5-", "gritlm"];

    //
    // The models from before chat completions existed. Providers keep offering some of them, and
    // Helmholtz Blablador still reports 'text-davinci-003', but asking any of them for a chat
    // completion fails. We deliberately do not look for 'ada' here: three letters appear in far too
    // many unrelated model names, and losing a chat model weighs heavier than keeping a dead one.
    //
    private static readonly string[] TEXT_COMPLETION_MARKERS = ["davinci", "babbage", "curie", "gpt-3.5-turbo-instruct"];

    private static readonly string[] IMAGE_GENERATION_MARKERS = ["flux", "stable-diffusion", "sdxl", "dall-e", "midjourney", "gpt-image"];

    private static readonly string[] VIDEO_GENERATION_MARKERS = ["sora", "veo-", "kling", "runway"];

    //
    // Voxtral is marketed as an audio model which understands speech, so one could expect it to work
    // in a chat as well. It does not: asking Mistral for a chat completion with 'voxtral-mini-latest'
    // is answered with 'Invalid model'. Voxtral therefore belongs here, next to the models which do
    // nothing but transcribe.
    //
    private static readonly string[] TRANSCRIPTION_MARKERS = ["whisper", "-transcribe", "wav2vec", "parakeet", "voxtral"];

    //
    // Besides the pure text-to-speech models, this covers the models which answer in audio, such as
    // 'gpt-audio' and 'gpt-4o-audio-preview'. Those do accept a text-only request, but they are made
    // for spoken conversations, and the providers offering them directly keep them out of their chat
    // model lists as well.
    //
    private static readonly string[] SPEECH_SYNTHESIS_MARKERS = ["-tts", "tts-", "-speech", "speech-", "-audio", "audio-"];

    //
    // The models for spoken conversations over a live connection. They speak their own protocol,
    // usually a WebSocket, and answer a chat completion request with an error. Checked before
    // transcription, because some of them carry the name of a transcription model, such as
    // OpenAI's 'gpt-realtime-whisper'. Those still need the live connection.
    //
    private static readonly string[] REALTIME_MARKERS = ["realtime"];

    private static readonly string[] OCR_MARKERS = ["ocr"];

    private static readonly string[] MODERATION_MARKERS = ["moderation", "guard"];

    /// <summary>
    /// Determines what kind of model this is, based on its name.
    /// </summary>
    /// <param name="model">The model to inspect.</param>
    /// <returns>The recognized kind, or ModelKind.CHAT when we recognize no other kind.</returns>
    public static ModelKind DetermineKind(this Model model)
    {
        if (string.IsNullOrWhiteSpace(model.Id) || model.IsSystemModel)
            return ModelKind.CHAT;

        if (HasAnyMarker(model.Id, OTHER_MARKERS))
            return ModelKind.OTHER;

        if (HasAnyMarker(model.Id, RERANKING_MARKERS))
            return ModelKind.RERANKING;

        if (HasAnyMarker(model.Id, EMBEDDING_MARKERS))
            return ModelKind.EMBEDDING;

        if (HasAnyMarker(model.Id, TEXT_COMPLETION_MARKERS))
            return ModelKind.TEXT_COMPLETION;

        if (HasAnyMarker(model.Id, IMAGE_GENERATION_MARKERS))
            return ModelKind.IMAGE_GENERATION;

        if (HasAnyMarker(model.Id, VIDEO_GENERATION_MARKERS))
            return ModelKind.VIDEO_GENERATION;

        if (HasAnyMarker(model.Id, REALTIME_MARKERS))
            return ModelKind.REALTIME;

        if (HasAnyMarker(model.Id, TRANSCRIPTION_MARKERS))
            return ModelKind.TRANSCRIPTION;

        if (HasAnyMarker(model.Id, SPEECH_SYNTHESIS_MARKERS))
            return ModelKind.SPEECH_SYNTHESIS;

        if (HasAnyMarker(model.Id, OCR_MARKERS))
            return ModelKind.OCR;

        if (HasAnyMarker(model.Id, MODERATION_MARKERS))
            return ModelKind.MODERATION;

        return ModelKind.CHAT;
    }

    /// <summary>
    /// Checks whether this model can be used for chatting.
    /// </summary>
    /// <param name="model">The model to check.</param>
    /// <returns>True, when the model is a chat model or when we recognize no other kind.</returns>
    public static bool IsChatModel(this Model model) => model.DetermineKind() is ModelKind.CHAT;

    /// <summary>
    /// Checks whether this model creates embeddings.
    /// </summary>
    /// <param name="model">The model to check.</param>
    /// <returns>True, when the model is an embedding model.</returns>
    public static bool IsEmbeddingModel(this Model model) => model.DetermineKind() is ModelKind.EMBEDDING;

    /// <summary>
    /// Checks whether this model transcribes audio.
    /// </summary>
    /// <param name="model">The model to check.</param>
    /// <returns>True, when the model is a transcription model.</returns>
    public static bool IsTranscriptionModel(this Model model) => model.DetermineKind() is ModelKind.TRANSCRIPTION;

    /// <summary>
    /// Checks whether this model generates images.
    /// </summary>
    /// <param name="model">The model to check.</param>
    /// <returns>True, when the model is an image generation model.</returns>
    public static bool IsImageModel(this Model model) => model.DetermineKind() is ModelKind.IMAGE_GENERATION;

    private static bool HasAnyMarker(string modelId, string[] markers)
    {
        foreach (var marker in markers)
            if (modelId.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}