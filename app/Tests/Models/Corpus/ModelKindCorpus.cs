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

        // Mistral's embedding checkpoint for code. It carries the name of a family which is a text
        // completion model on this very provider, and stays an embedding model regardless: what a
        // model is for is said by the word which says it, not by the family it was built from.
        new(MISTRAL, "codestral-embed", EMBEDDING),

        //
        // What a local Ollama installation serves, taken off its models endpoint rather than
        // written from memory. The last two are the ones worth having: neither name carries the
        // word "embed", so both were lost by the phrase the self-hosted provider used to filter
        // with, and turned up among the chat models instead. Here they are answered by "bge" and
        // "minilm", which is what those words are written for.
        //
        new(SELF_HOSTED, "qwen3-embedding:0.6b", EMBEDDING),
        new(SELF_HOSTED, "qwen3-embedding:latest", EMBEDDING),
        new(SELF_HOSTED, "nomic-embed-text:latest", EMBEDDING),
        new(SELF_HOSTED, "bge-m3:latest", EMBEDDING),
        new(SELF_HOSTED, "all-minilm:latest", EMBEDDING),

        //
        // The four whose names say nothing about embedding at all. They are here because the list
        // above answers them through a word they happen to carry, and these carry none: without a
        // rule of their own they would count as chat models, which is where they stood.
        //
        new(SELF_HOSTED, "stella_en_400M_v5", EMBEDDING),
        new(SELF_HOSTED, "LaBSE", EMBEDDING),
        new(SELF_HOSTED, "instructor-xl", EMBEDDING),
        new(SELF_HOSTED, "gtr-t5-large", EMBEDDING),
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

        // NVIDIA's other speech line, once plain and once as the hub names it. The second one is
        // what makes the rule a segment worth keeping: the organization comes off before any rule
        // sees the name, so what is left has to carry the word on its own.
        new(SELF_HOSTED, "canary-1b-flash", TRANSCRIPTION),
        new(SELF_HOSTED, "nvidia/canary-180m-flash", TRANSCRIPTION),

        //
        // Alibaba's speech line. Every one of these begins with the letter the Alibaba Cloud
        // provider kept its whole chat list by, so all of them stood among the models somebody
        // talks to. The last one carries two words at once, and the one which decides is not the
        // longer one.
        //
        new(ALIBABA_CLOUD, "qwen3-asr-flash", TRANSCRIPTION),
        new(ALIBABA_CLOUD, "qwen3-asr-1.7b", TRANSCRIPTION),
        new(ALIBABA_CLOUD, "qwen-audio-3.0-asr-flash-streaming", TRANSCRIPTION),
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

        new(ALIBABA_CLOUD, "qwen-tts", SPEECH_SYNTHESIS),
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

        //
        // Alibaba builds the word into both of its speech lines, and both are here to hold the
        // rank the realtime rule carries: a connection AI Studio cannot open stays out of every
        // list, whether the model would otherwise have spoken or listened.
        //
        new(ALIBABA_CLOUD, "qwen-tts-realtime", REALTIME),
        new(ALIBABA_CLOUD, "qwen3-asr-flash-realtime", REALTIME),
    ];

    /// <summary>
    /// The models which work a screen.
    /// </summary>
    private static readonly ModelKindExample[] COMPUTER_USE_ENTRIES =
    [
        new(GOOGLE, "gemini-2.5-computer-use-preview-10-2025", COMPUTER_USE, AnsweredTodayAs: CHAT, Reason: "Found in the chat list while testing. Its API refuses every request which does not carry the computer use tool, so a conversation with it cannot even begin."),
    ];

    /// <summary>
    /// The models which continue a text instead of answering in a conversation.
    /// </summary>
    private static readonly ModelKindExample[] TEXT_COMPLETION_ENTRIES =
    [
        new(HELMHOLTZ, "text-davinci-003", TEXT_COMPLETION),
        new(OPEN_AI, "babbage-002", TEXT_COMPLETION),
        new(OPEN_AI, "gpt-3.5-turbo-instruct", TEXT_COMPLETION),

        // The one of these which is not old: Mistral serves Codestral to fill in the middle of a
        // file. It says so only for Mistral's own catalog, which is why the open weights of the
        // same name stay a chat model further down.
        new(MISTRAL, "codestral-latest", TEXT_COMPLETION),
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

        // The open weights of the model Mistral itself serves to fill in the middle of a file.
        // Whoever runs them runs them behind a chat completion API, so here the name means
        // something to talk to -- which is what binding that other rule to Mistral protects.
        new(SELF_HOSTED, "codestral-22b-v0.1", CHAT),

        // The other half of that same Ollama installation, and the pair which makes the point:
        // qwen3.8 and qwen3-embedding are one family and two answers. A rule written to select
        // rather than to modify would have to beat the family name to get there.
        new(SELF_HOSTED, "qwen3.8:latest", CHAT),
        new(SELF_HOSTED, "gpt-oss:latest", CHAT),

        // Half the chat models of the world carry this word, and one of the embedding names above
        // is one letter longer than it. A name part is what keeps the two apart:
        new(SELF_HOSTED, "mistral-7b-instruct", CHAT),

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

}