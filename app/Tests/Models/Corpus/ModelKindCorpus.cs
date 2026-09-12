using AIStudio.Provider;

using static AIStudio.Provider.LLMProviders;
using static AIStudio.Provider.ModelKind;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// The names which say what a model is made for.
/// </summary>
/// <remarks>
/// A list of its own, next to the corpus the capability rules are measured against. The two answer
/// different questions and are made of different names: the capability corpus is full of chat models,
/// because everything else has no capabilities worth stating, while every name here is one nobody
/// should be able to start a conversation with.
///
/// Each entry says what the model is for. Where the markers being replaced answer something else,
/// the entry says that too, with the reason -- so that the port can be held to changing nothing
/// except where somebody decided it should.
/// </remarks>
public static class ModelKindCorpus
{
    /// <summary>
    /// The models which turn text into a vector.
    /// </summary>
    private static readonly ModelKindExample[] EMBEDDING_ENTRIES =
    [
        new(OPEN_AI, "text-embedding-3-small", EMBEDDING),
        new(SELF_HOSTED, "mxbai-embed-large:latest", EMBEDDING),
        new(SELF_HOSTED, "bge-m3:567m", EMBEDDING),
        new(SELF_HOSTED, "multilingual-e5-large", EMBEDDING),
        new(SELF_HOSTED, "gte-multilingual-base", EMBEDDING),
        new(SELF_HOSTED, "paraphrase-multilingual-mpnet-base-v2", EMBEDDING),
        new(SELF_HOSTED, "gritlm-7b", EMBEDDING),

        // The one name whose only marker used to be the organization it was published under:
        new(SELF_HOSTED, "sentence-transformers/all-MiniLM-L6-v2", EMBEDDING),
    ];

    /// <summary>
    /// The models which put search results back into order, each named after an embedding model.
    /// </summary>
    private static readonly ModelKindExample[] RERANKING_ENTRIES =
    [
        new(SELF_HOSTED, "bge-reranker-v2-m3", RERANKING),
        new(SELF_HOSTED, "gte-multilingual-reranker-base", RERANKING),
        new(SELF_HOSTED, "qwen3-reranker-8b", RERANKING),
    ];

    /// <summary>
    /// The models which draw.
    /// </summary>
    private static readonly ModelKindExample[] IMAGE_ENTRIES =
    [
        new(OPEN_AI, "gpt-image-1", IMAGE_GENERATION),
        new(OPEN_AI, "dall-e-3", IMAGE_GENERATION),
        new(SELF_HOSTED, "flux.1-schnell", IMAGE_GENERATION),
        new(SELF_HOSTED, "stable-diffusion-3.5-large", IMAGE_GENERATION),
        new(GOOGLE, "gemini-3-pro-image", IMAGE_GENERATION),

        new(GOOGLE, "imagen-4.0-generate-001", IMAGE_GENERATION, AnsweredTodayAs: CHAT, Reason: "The markers never knew the name; the family ported in the Google step states it. Nobody noticed because the Google provider shows only names beginning with gemini."),
    ];

    /// <summary>
    /// The models which make video.
    /// </summary>
    private static readonly ModelKindExample[] VIDEO_ENTRIES =
    [
        new(OPEN_AI, "sora-2", VIDEO_GENERATION),
        new(GOOGLE, "veo-3.0-generate-001", VIDEO_GENERATION),
        new(SELF_HOSTED, "kling-video-v2", VIDEO_GENERATION),

        new(X, "grok-imagine-video", VIDEO_GENERATION, AnsweredTodayAs: CHAT, Reason: "Found in the chat list while testing. No marker knew the name, and the xAI provider only kept models whose name lacks \"-image\" -- which \"-imagine\" does."),
        new(X, "grok-imagine-video-1.5", VIDEO_GENERATION, AnsweredTodayAs: CHAT, Reason: "The same, one version on."),
    ];

    /// <summary>
    /// The models which listen and write down what they heard.
    /// </summary>
    private static readonly ModelKindExample[] TRANSCRIPTION_ENTRIES =
    [
        new(OPEN_AI, "gpt-4o-transcribe", TRANSCRIPTION),
        new(SELF_HOSTED, "faster-whisper-large-v3", TRANSCRIPTION),
        new(SELF_HOSTED, "parakeet-tdt-0.6b-v2", TRANSCRIPTION),
        new(SELF_HOSTED, "wav2vec2-large-xlsr-53", TRANSCRIPTION),
        new(MISTRAL, "voxtral-mini-latest", TRANSCRIPTION),
    ];

    /// <summary>
    /// The models which speak, and the ones which answer in audio.
    /// </summary>
    private static readonly ModelKindExample[] SPEECH_ENTRIES =
    [
        new(OPEN_AI, "tts-1-hd", SPEECH_SYNTHESIS),
        new(OPEN_AI, "gpt-4o-mini-tts", SPEECH_SYNTHESIS),
        new(OPEN_AI, "gpt-audio", SPEECH_SYNTHESIS),
        new(OPEN_AI, "gpt-4o-audio-preview", SPEECH_SYNTHESIS),

        // The one name which glues the word to something else, and the reason the three words are
        // not loosened into substrings:
        new(SELF_HOSTED, "xtts-v2", SPEECH_SYNTHESIS),
    ];

    /// <summary>
    /// The models which want a connection of their own.
    /// </summary>
    private static readonly ModelKindExample[] REALTIME_ENTRIES =
    [
        new(OPEN_AI, "gpt-realtime", REALTIME),
        new(OPEN_AI, "gpt-4o-realtime-preview", REALTIME),

        // The name the marker file names as the reason for asking this question before the others:
        new(OPEN_AI, "gpt-realtime-whisper", REALTIME),

        new(OPEN_AI, "gpt-live-1", REALTIME, AnsweredTodayAs: CHAT, Reason: "Found in the chat list while testing. The line which succeeds the realtime models dropped the word, and it is even less of a chat partner: it listens and speaks at once and leaves the thinking to a text model behind it."),
    ];

    /// <summary>
    /// The models which work a screen.
    /// </summary>
    private static readonly ModelKindExample[] COMPUTER_USE_ENTRIES =
    [
        new(GOOGLE, "gemini-2.5-computer-use-preview-10-2025", COMPUTER_USE, AnsweredTodayAs: CHAT, Reason: "Found in the chat list while testing. Its API refuses every request which does not carry the computer use tool, so a conversation with it cannot even begin."),
    ];

    /// <summary>
    /// The models from before chat completions existed.
    /// </summary>
    private static readonly ModelKindExample[] TEXT_COMPLETION_ENTRIES =
    [
        new(HELMHOLTZ, "text-davinci-003", TEXT_COMPLETION),
        new(OPEN_AI, "babbage-002", TEXT_COMPLETION),
        new(OPEN_AI, "gpt-3.5-turbo-instruct", TEXT_COMPLETION),
    ];

    /// <summary>
    /// The models which read text off a page.
    /// </summary>
    private static readonly ModelKindExample[] OCR_ENTRIES =
    [
        new(MISTRAL, "mistral-ocr-latest", OCR),
    ];

    /// <summary>
    /// The models which judge content instead of writing it.
    /// </summary>
    private static readonly ModelKindExample[] MODERATION_ENTRIES =
    [
        new(OPEN_AI, "omni-moderation-latest", MODERATION),
        new(SELF_HOSTED, "llama-guard-3-8b", MODERATION),

        // Written without a separator, which is why the word is looked for as a plain substring:
        new(SELF_HOSTED, "Qwen3Guard-Gen-8B", MODERATION),
    ];

    /// <summary>
    /// The entries which are no models at all.
    /// </summary>
    private static readonly ModelKindExample[] NOT_A_MODEL_ENTRIES =
    [
        new(OPEN_AI, "container", OTHER),
    ];

    /// <summary>
    /// The names which carry a word of one of the kinds above without being one.
    /// </summary>
    /// <remarks>
    /// These are the reason several of the words are looked for as whole name parts. A model sorted
    /// into the wrong kind disappears from the user's list, and a fine-tune losing its place because
    /// somebody named it after Star Trek is exactly the kind of defect nobody goes looking for.
    /// </remarks>
    private static readonly ModelKindExample[] STILL_CHAT_MODELS =
    [
        new(SELF_HOSTED, "llama-2-7b-chat-klingon", CHAT),
        new(SELF_HOSTED, "llama3.3:70b", CHAT),
        new(OPEN_AI, "gpt-5.1", CHAT),

        //
        // Three which were questioned while testing and stay all the same. Grok Build is the coding
        // model behind the xAI CLI and answers like any other Grok. The Groq compound systems are
        // models with tools already built in, reached through the ordinary chat completion API. And
        // Gemini Robotics ER answers in text; it is built for pointing at things in a picture rather
        // than for conversation, but a conversation with it works, and a model which works belongs
        // in the list.
        //
        new(X, "grok-build-0.1", CHAT),
        new(GROQ, "groq/compound", CHAT),
        new(GROQ, "groq/compound-mini", CHAT),
        new(GOOGLE, "gemini-robotics-er-1.5-preview", CHAT),
    ];

    /// <summary>
    /// Every example, in the order the kinds are written above.
    /// </summary>
    public static readonly IReadOnlyList<ModelKindExample> ENTRIES =
    [
        ..EMBEDDING_ENTRIES,
        ..RERANKING_ENTRIES,
        ..IMAGE_ENTRIES,
        ..VIDEO_ENTRIES,
        ..TRANSCRIPTION_ENTRIES,
        ..SPEECH_ENTRIES,
        ..REALTIME_ENTRIES,
        ..COMPUTER_USE_ENTRIES,
        ..TEXT_COMPLETION_ENTRIES,
        ..OCR_ENTRIES,
        ..MODERATION_ENTRIES,
        ..NOT_A_MODEL_ENTRIES,
        ..STILL_CHAT_MODELS,
    ];

    /// <summary>
    /// What the markers being replaced answer for a name, where that is not what the rules answer.
    /// </summary>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="modelId">The model ID as that provider reports it.</param>
    /// <returns>The old answer, or null when nobody recorded a difference for this name.</returns>
    public static ModelKind? AnsweredTodayAs(LLMProviders provider, string modelId) => ENTRIES
        .FirstOrDefault(example => example.Provider == provider && string.Equals(example.ModelId, modelId, StringComparison.Ordinal))
        ?.AnsweredTodayAs;
}