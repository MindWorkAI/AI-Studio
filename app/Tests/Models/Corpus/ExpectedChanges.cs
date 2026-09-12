using static AIStudio.Provider.Capability;
using static AIStudio.Provider.LLMProviders;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// The answers the rebuild has to change, one entry per model.
/// </summary>
/// <remarks>
/// Every entry here was found by running the corpus against the rules as they stand and reading
/// what came back. They are kept out of the snapshot so that the rebuild does not copy them: a
/// snapshot says "do not change this", and a wrong answer is the one thing that must change.
///
/// Three kinds of mistake are collected below, and they are the three the new architecture is meant
/// to make impossible rather than fix one by one:
///
/// - A model which is not a chat model at all is answered as if it were one. The app already knows
///   better: it asks its providers for embedding and transcription models through methods of their
///   own. The capability rules never hear about that and hand out tool calling and image input.
/// - The same model gets two different answers depending on which spelling it arrives in. That is
///   the routing graph leaking into the rules, and it is what the explicit hosts are for.
/// - A prefix rule swallows a variant whose name says the opposite. That is priority written by
///   hand, and it is what computed specificity is for.
/// </remarks>
public static class ExpectedChanges
{
    /// <summary>
    /// Where the app itself states that a model is not a chat model.
    /// </summary>
    private const string THE_APP_LISTS_IT_AS_AN_EMBEDDING_MODEL = "The app asks every provider for its embedding models separately, through IProvider.GetEmbeddingModels.";

    /// <summary>
    /// Every model whose answer has to change.
    /// </summary>
    public static readonly IReadOnlyList<ExpectedChange> ENTRIES =
    [
        //
        // Embedding models. They turn text into a vector; there is nothing for them to call a
        // function with and no image for them to look at. What they need stated is that they embed,
        // which the capability vocabulary has a word for and the rules never use.
        //
        new(OPEN_AI, "text-embedding-3-large",
            AnswerToday: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, RESPONSES_API, WEB_SEARCH],
            AnswerWanted: [TEXT_INPUT, EMBEDDING],
            Reason: "An embedding model is answered with the OpenAI chat default, tool calling and image input included.",
            Source: THE_APP_LISTS_IT_AS_AN_EMBEDDING_MODEL),

        new(GOOGLE, "text-embedding-004",
            AnswerToday: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, EMBEDDING],
            Reason: "An embedding model is answered with the Google default for everything which is not a Gemini.",
            Source: THE_APP_LISTS_IT_AS_AN_EMBEDDING_MODEL),

        new(ALIBABA_CLOUD, "text-embedding-v3",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, EMBEDDING],
            Reason: "An embedding model is answered with the Alibaba default, because its name starts with none of the Qwen prefixes.",
            Source: "Provider/AlibabaCloud/ProviderAlibabaCloud.cs adds it in GetEmbeddingModels and filters the catalog by the prefix \"text-embedding-\"."),

        new(SELF_HOSTED, "nomic-embed-text:latest",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, EMBEDDING],
            Reason: "An embedding model reaches the global fallback, which assumes an instruction-tuned model that calls functions.",
            Source: THE_APP_LISTS_IT_AS_AN_EMBEDDING_MODEL),

        new(SELF_HOSTED, "granite-embedding:278m",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, EMBEDDING],
            Reason: "The Granite block answers about a checkpoint which embeds, and it hands out tool calling for it.",
            Source: THE_APP_LISTS_IT_AS_AN_EMBEDDING_MODEL),

        new(GWDG, "e5-mistral-7b-instruct",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, EMBEDDING],
            Reason: "An embedding model is judged by the Mistral rules, because its name carries the word.",
            Source: THE_APP_LISTS_IT_AS_AN_EMBEDDING_MODEL),

        //
        // Transcription models. They take speech and write it down. Three of the four are in the
        // app's own list of transcription models, with the provider's documentation next to them.
        //
        new(OPEN_AI, "whisper-1",
            AnswerToday: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, RESPONSES_API, WEB_SEARCH],
            AnswerWanted: [SPEECH_INPUT, TEXT_OUTPUT],
            Reason: "A transcription model is answered with the OpenAI chat default, web search and image input included.",
            Source: "The app asks every provider for its transcription models separately, through IProvider.GetTranscriptionModels."),

        new(FIREWORKS, "whisper-v3",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [SPEECH_INPUT, TEXT_OUTPUT],
            Reason: "A transcription model reaches the global fallback and is told it calls functions.",
            Source: "Provider/Fireworks/ProviderFireworks.cs returns it from GetTranscriptionModels."),

        new(GWDG, "whisper-large-v2",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [SPEECH_INPUT, TEXT_OUTPUT],
            Reason: "A transcription model reaches the global fallback and is told it calls functions.",
            Source: "Provider/GWDG/ProviderGWDG.cs returns it from GetTranscriptionModels."),

        new(GROQ, "whisper-large-v3-turbo",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [SPEECH_INPUT, TEXT_OUTPUT],
            Reason: "A transcription model reaches the global fallback and is told it calls functions.",
            Source: "Same model family as the Whisper entries the app lists for Fireworks and GWDG."),

        //
        // An image generation model. It draws a picture from a description; there is no
        // conversation in it and nothing to call a function with.
        //
        new(GOOGLE, "imagen-4.0-generate-001",
            AnswerToday: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, IMAGE_OUTPUT],
            Reason: "An image generation model is answered with the Google default for everything which is not a Gemini: it is told it reads images, writes text, and calls functions, and the one thing it does is not said at all.",
            Source: "Provider/Google/ProviderGoogle.cs keeps only names beginning with \"gemini-\" in its chat model list, so this model is never a chat model to begin with; Provider/ModelKindExtensions.cs classifies image generation separately."),

        //
        // One model, two spellings, two answers.
        //
        new(LITE_LLM, "bedrock/anthropic.claude-3-5-sonnet-20241022-v2:0",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            Reason: "Claude 3.5 Sonnet loses its image input when it arrives under the Bedrock spelling: the vendor sits behind a dot rather than a slash, so neither the gateway detection nor the reseller check finds it.",
            Source: "The same model as \"anthropic/claude-sonnet-4-0\" and the other Claude entries of this corpus, which all report image input."),

        new(SELF_HOSTED, "mistral-small-3.1-24b-instruct",
            AnswerToday: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, OPTIONAL_REASONING, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            Reason: "Mistral Small 3.1 is told that it thinks, and the very same model is told the opposite when it arrives through Mistral's own API. The rules for the open weights answer for the whole 3 and 4 range in one line, and reasoning arrived with 4.",
            Source: "The corpus entry \"mistral-small-2503\" is this model at Mistral and reports no reasoning; Mistral names Magistral as the thinking model of that generation."),

        new(HELMHOLTZ, "01 - GPT-5.5 - great overall performance",
            AnswerToday: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, WEB_SEARCH, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, REASONING_BY_DEFAULT, WEB_SEARCH, CHAT_COMPLETION_API],
            Reason: "The descriptive name is recognized as a GPT model and then placed nowhere: every version rule matches the beginning of the name, which here is the list number. The model loses the reasoning it is known for.",
            Source: "The GWDG entry \"gpt-5.5\" of this corpus is the same model and does report reasoning by default."),

        //
        // A rule written for one spelling of a name, while the engine people actually run writes
        // another. The rule is right about the model and never fires.
        //
        new(SELF_HOSTED, "granite4.2:8b",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, TEXT_OUTPUT, REASONING_BY_DEFAULT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            Reason: "Granite 4.2 thinks unless the request says otherwise, and there is a rule which says so -- for \"granite-4.2\". Ollama glues the version to the family name, so the rule never sees the models anybody runs locally.",
            Source: "IBM documents thinking on by default from Granite 4.2; the Ollama library lists the same checkpoint as \"granite4.2\"."),

        new(SELF_HOSTED, "granite3.3:8b",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, TEXT_OUTPUT, OPTIONAL_REASONING, FUNCTION_CALLING, CHAT_COMPLETION_API],
            Reason: "The same spelling problem one generation earlier: Granite 3.3 has a thinking toggle, and the rule for it is written as \"granite-3.3\".",
            Source: "IBM documents the thinking toggle for Granite 3.2 and 3.3; the Ollama library lists the checkpoint as \"granite3.3\"."),

        //
        // One vendor's block swallowing another vendor's model, for no reason but where the two
        // blocks stand in the file.
        //
        new(SELF_HOSTED, "llama-3.3-nemotron-super-49b",
            AnswerToday: [TEXT_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, CHAT_COMPLETION_API],
            AnswerWanted: [TEXT_INPUT, TEXT_OUTPUT, OPTIONAL_REASONING, FUNCTION_CALLING, CHAT_COMPLETION_API],
            Reason: "An NVIDIA model is answered by the Llama rules because it carries the name of the weights it was built from, and the Llama block stands above the Nemotron one. It loses the thinking switch, which is one of the two things NVIDIA changed about those weights.",
            Source: "The corpus entry \"nemotron-3-49b\" is the generation after it and does report thinking; NVIDIA documents the detailed thinking switch for the Llama-Nemotron models."),

        //
        // A prefix rule swallowing the variant which says the opposite.
        //
        new(OPEN_AI, "gpt-5-chat-latest",
            AnswerToday: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, ALWAYS_REASONING, WEB_SEARCH, RESPONSES_API],
            AnswerWanted: [TEXT_INPUT, MULTIPLE_IMAGE_INPUT, TEXT_OUTPUT, FUNCTION_CALLING, WEB_SEARCH, RESPONSES_API],
            Reason: "The alias for the non-reasoning GPT-5 is claimed by the \"gpt-5-\" prefix rule and is told it always reasons, which is the one thing its name rules out.",
            Source: "OpenAI names this alias as the non-reasoning model of the GPT-5 line; the corpus entry \"gpt-5\" next to it is the reasoning one."),
    ];
}