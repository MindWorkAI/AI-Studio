using static AIStudio.Provider.LLMProviders;
using static AIStudio.Tests.Models.Corpus.CorpusOrigin;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// The model IDs the capability rules are measured against.
/// </summary>
/// <remarks>
/// This list is the ruler for rebuilding the capability system. Every entry is a name some provider
/// really answers with, together with the provider it arrives from, because the same model gets a
/// different answer depending on who serves it: an ID travels through the rules of its host before
/// it reaches the rules of its family.
///
/// Two things make an entry worth having. Either it is the only name that reaches a particular
/// rule, so removing it would let that rule rot unnoticed. Or it is a name no rule was written for,
/// which is what the fallback exists for and what nobody looks at otherwise. Names that merely vary
/// a size or a date are left out; they exercise the same rule twice and only make the snapshot
/// longer.
///
/// Sizes, dates, and quantization suffixes appear where they change the answer, and only there.
/// </remarks>
public static class ModelCorpus
{
    /// <summary>
    /// OpenAI, reached directly. Its rules are the only ones that hand out the Responses API.
    /// </summary>
    private static readonly CorpusEntry[] OPEN_AI_ENTRIES =
    [
        new(OPEN_AI, "gpt-6-astra", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-6-astra-mini", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-5.6", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-5.5", ON_THE_MANUAL_TEST_LIST),
        new(OPEN_AI, "gpt-5.4", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-5.3", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-5.2", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-5.1", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-5.1-codex", NAMED_BY_NO_RULE),
        new(OPEN_AI, "gpt-5", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-5-mini", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-5-nano", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-5-chat-latest", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-4o", NAMED_BY_NO_RULE),
        new(OPEN_AI, "gpt-4o-mini", NAMED_BY_NO_RULE),
        new(OPEN_AI, "gpt-4o-search-preview", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-4o-mini-search-preview", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-4o-audio-preview", NAMED_BY_NO_RULE),
        new(OPEN_AI, "gpt-4-turbo", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-4", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-4-0613", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-3.5-turbo", NAMED_BY_A_RULE),
        new(OPEN_AI, "gpt-3.5-turbo-16k", NAMED_BY_A_RULE),
        new(OPEN_AI, "o1", NAMED_BY_A_RULE),
        new(OPEN_AI, "o1-pro", NAMED_BY_A_RULE),
        new(OPEN_AI, "o1-mini", NAMED_BY_A_RULE),
        new(OPEN_AI, "o3", NAMED_BY_A_RULE),
        new(OPEN_AI, "o3-pro", NAMED_BY_A_RULE),
        new(OPEN_AI, "o3-mini", NAMED_BY_A_RULE),
        new(OPEN_AI, "o4-mini", NAMED_BY_A_RULE),
        new(OPEN_AI, "text-embedding-3-large", NAMED_BY_NO_RULE),
        new(OPEN_AI, "whisper-1", NAMED_BY_NO_RULE),
    ];

    /// <summary>
    /// Anthropic, reached directly. The six dated aliases come from the list the app falls back to.
    /// </summary>
    private static readonly CorpusEntry[] ANTHROPIC_ENTRIES =
    [
        new(ANTHROPIC, "claude-mythos-5", NAMED_BY_A_RULE),
        new(ANTHROPIC, "claude-fable-5-1", NAMED_BY_A_RULE),
        new(ANTHROPIC, "claude-opus-5", NAMED_BY_A_RULE),
        new(ANTHROPIC, "claude-sonnet-5", ON_THE_MANUAL_TEST_LIST),
        new(ANTHROPIC, "claude-haiku-4-5-20251001", NAMED_BY_A_RULE),
        new(ANTHROPIC, "claude-opus-4-0", BUILT_INTO_THE_APP),
        new(ANTHROPIC, "claude-sonnet-4-0", BUILT_INTO_THE_APP),
        new(ANTHROPIC, "claude-3-7-sonnet-latest", BUILT_INTO_THE_APP),
        new(ANTHROPIC, "claude-3-5-sonnet-latest", BUILT_INTO_THE_APP),
        new(ANTHROPIC, "claude-3-5-haiku-latest", BUILT_INTO_THE_APP),
        new(ANTHROPIC, "claude-3-opus-latest", BUILT_INTO_THE_APP),
        new(ANTHROPIC, "claude-7-sonnet", NAMED_BY_NO_RULE),
    ];

    /// <summary>
    /// Google, reached directly. Everything hangs on whether the name carries "gemini-" at all.
    /// </summary>
    private static readonly CorpusEntry[] GOOGLE_ENTRIES =
    [
        new(GOOGLE, "gemini-3-pro", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-3-flash", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-3-pro-image", ON_THE_MANUAL_TEST_LIST),
        new(GOOGLE, "gemini-3.1-flash-image", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-flash-latest", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-pro-latest", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-2.5-pro", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-2.5-flash", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-2.5-flash-lite", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-2.5-flash-image", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-2.0-flash", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-2.0-flash-live-001", NAMED_BY_A_RULE),
        new(GOOGLE, "gemini-1.0-pro-vision", NAMED_BY_A_RULE),
        new(GOOGLE, "text-embedding-004", NAMED_BY_NO_RULE),
        new(GOOGLE, "imagen-4.0-generate-001", NAMED_BY_NO_RULE),
    ];

    /// <summary>
    /// Mistral, reached directly. The family is versioned by release date, so the dated names are
    /// what the rules really read; the marketing names are a table on the side.
    /// </summary>
    private static readonly CorpusEntry[] MISTRAL_ENTRIES =
    [
        new(MISTRAL, "mistral-large-latest", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-large-2512", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-large-2411", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-medium-latest", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-medium-2604", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-medium-2508", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-medium-2505", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-medium-3.5", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-medium-3-5", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-small-latest", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-small-2603", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-small-2503", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-small-2501", NAMED_BY_A_RULE),
        new(MISTRAL, "ministral-3b-latest", NAMED_BY_A_RULE),
        new(MISTRAL, "ministral-8b-2410", NAMED_BY_A_RULE),
        new(MISTRAL, "ministral-14b-2512", QUOTED_AS_A_NAME_SHAPE),
        new(MISTRAL, "pixtral-large-latest", NAMED_BY_A_RULE),
        new(MISTRAL, "pixtral-12b-2409", NAMED_BY_A_RULE),
        new(MISTRAL, "mistral-saba-2502", NAMED_BY_A_RULE),
        new(MISTRAL, "magistral-medium-2506", NAMED_BY_NO_RULE),
        new(MISTRAL, "voxtral-small-2507", NAMED_BY_NO_RULE),
        new(MISTRAL, "codestral-2508", NAMED_BY_NO_RULE),
        new(MISTRAL, "open-mistral-nemo", NAMED_BY_NO_RULE),
    ];

    /// <summary>
    /// Alibaba Cloud. Everything below the two dozen models the app carries is one Qwen tier per
    /// entry, because each tier answers differently about thinking and vision.
    /// </summary>
    private static readonly CorpusEntry[] ALIBABA_ENTRIES =
    [
        new(ALIBABA_CLOUD, "qwq-plus", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen-max-latest", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen-plus-latest", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen-turbo-latest", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qvq-max", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen-vl-max", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen-mt-plus", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen2.5-72b-instruct", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen2.5-14b-instruct-1m", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen2.5-omni-7b", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen2.5-vl-72b-instruct", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "text-embedding-v3", BUILT_INTO_THE_APP),
        new(ALIBABA_CLOUD, "qwen3-omni-flash", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3-vl-plus", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3-235b-a22b", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3.5-plus", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3.6-max", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3.7-max", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3.7-max-preview", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3.7-max-2026-05-17", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3.7-max-2026-06-08", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3.8-flash", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3.8-max", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwen3.8-27b", NAMED_BY_A_RULE),
        new(ALIBABA_CLOUD, "qwq-32b", ON_THE_MANUAL_TEST_LIST),
    ];

    /// <summary>
    /// The DeepSeek platform. Two of its names are aliases of their own; everything else is the open
    /// weights under their published name, which is why the rules hand those on.
    /// </summary>
    private static readonly CorpusEntry[] DEEP_SEEK_ENTRIES =
    [
        new(DEEP_SEEK, "deepseek-chat", NAMED_BY_A_RULE),
        new(DEEP_SEEK, "deepseek-reasoner", NAMED_BY_A_RULE),
        new(DEEP_SEEK, "deepseek-v3.2-exp", NAMED_BY_A_RULE),
        new(DEEP_SEEK, "deepseek-v4", NAMED_BY_A_RULE),
        new(DEEP_SEEK, "deepseek-v4-vision", NAMED_BY_A_RULE),
    ];

    /// <summary>
    /// Perplexity. One rule separates the thinking Sonar models from the rest.
    /// </summary>
    private static readonly CorpusEntry[] PERPLEXITY_ENTRIES =
    [
        new(PERPLEXITY, "sonar", NAMED_BY_NO_RULE),
        new(PERPLEXITY, "sonar-pro", NAMED_BY_NO_RULE),
        new(PERPLEXITY, "sonar-reasoning", NAMED_BY_A_RULE),
        new(PERPLEXITY, "sonar-reasoning-pro", NAMED_BY_A_RULE),
        new(PERPLEXITY, "sonar-deep-research", NAMED_BY_A_RULE),
    ];

    /// <summary>
    /// xAI. It is served by the rules for open weights, which is where the Grok block lives.
    /// </summary>
    private static readonly CorpusEntry[] XAI_ENTRIES =
    [
        new(X, "grok-4", NAMED_BY_A_RULE),
        new(X, "grok-4-fast-reasoning", NAMED_BY_A_RULE),
        new(X, "grok-4.20", NAMED_BY_A_RULE),
        new(X, "grok-4.20-non-reasoning", NAMED_BY_A_RULE),
        new(X, "grok-3", NAMED_BY_A_RULE),
        new(X, "grok-3-mini", NAMED_BY_A_RULE),
        new(X, "grok-2-vision-1212", NAMED_BY_A_RULE),
        new(X, "grok-5", NAMED_BY_NO_RULE),
        new(X, "grok-build-0.1", NAMED_BY_A_RULE),
    ];

    /// <summary>
    /// The gateways, which name a model "vendor/model" and serve everything through the chat
    /// completion API. Each entry picks a different branch of the vendor detection.
    /// </summary>
    private static readonly CorpusEntry[] GATEWAY_ENTRIES =
    [
        new(OPEN_ROUTER, "openai/gpt-5.6", QUOTED_AS_A_NAME_SHAPE),
        new(OPEN_ROUTER, "openai/gpt-oss-120b", NAMED_BY_A_RULE),
        new(OPEN_ROUTER, "anthropic/claude-opus-5", QUOTED_AS_A_NAME_SHAPE),
        new(OPEN_ROUTER, "google/gemini-3.7-flash", QUOTED_AS_A_NAME_SHAPE),
        new(OPEN_ROUTER, "google/gemma-4-31b-it", NAMED_BY_A_RULE),
        new(OPEN_ROUTER, "mistralai/mistral-large-3", NAMED_BY_A_RULE),
        new(OPEN_ROUTER, "perplexity/sonar-reasoning", NAMED_BY_A_RULE),
        new(OPEN_ROUTER, "qwen/qwen3.8-flash-next", QUOTED_AS_A_NAME_SHAPE),
        new(OPEN_ROUTER, "deepseek/deepseek-r1-distill-llama-70b", ON_THE_MANUAL_TEST_LIST),
        new(OPEN_ROUTER, "deepseek/deepseek-chat-v3.1", NAMED_BY_A_RULE),
        new(OPEN_ROUTER, "moonshotai/kimi-k2-thinking", ON_THE_MANUAL_TEST_LIST),
        new(OPEN_ROUTER, "z-ai/glm-5.3", NAMED_BY_A_RULE),
        new(OPEN_ROUTER, "meta-llama/llama-4-maverick", NAMED_BY_A_RULE),
        new(OPEN_ROUTER, "nvidia/nemotron-3-49b", NAMED_BY_A_RULE),
        new(OPEN_ROUTER, "minimax/minimax-m2", NAMED_BY_A_RULE),
        new(LITE_LLM, "anthropic/claude-sonnet-5", QUOTED_AS_A_NAME_SHAPE),
        new(LITE_LLM, "azure/gpt-5.6", QUOTED_AS_A_NAME_SHAPE),
        new(LITE_LLM, "bedrock/anthropic.claude-3-5-sonnet-20241022-v2:0", NAMED_BY_NO_RULE),
        new(LITE_LLM, "the-fast-one", NAMED_BY_NO_RULE),
    ];

    /// <summary>
    /// Hugging Face. Two things have to come off before a name says anything: the routing suffix,
    /// which names the inference provider, and the organization in front of the slash.
    /// </summary>
    private static readonly CorpusEntry[] HUGGING_FACE_ENTRIES =
    [
        new(HUGGINGFACE, "google/gemma-4-31B-it:novita", QUOTED_AS_A_NAME_SHAPE),
        new(HUGGINGFACE, "meta-llama/Llama-4-Scout-17B-16E-Instruct", NAMED_BY_A_RULE),
        new(HUGGINGFACE, "meta-llama/Meta-Llama-3.1-405B-Instruct", QUOTED_AS_A_NAME_SHAPE),
        new(HUGGINGFACE, "Qwen/Qwen3.8-27B", NAMED_BY_A_RULE),
        new(HUGGINGFACE, "deepseek-ai/DeepSeek-R1-Distill-Qwen-32B", NAMED_BY_A_RULE),
        new(HUGGINGFACE, "openai/gpt-oss-120b:fireworks-ai", NAMED_BY_A_RULE),
        new(HUGGINGFACE, "mistralai/Magistral-Small-2509", NAMED_BY_A_RULE),
        new(HUGGINGFACE, "HuggingFaceTB/SmolLM3-3B", NAMED_BY_A_RULE),
    ];

    /// <summary>
    /// Providers that serve other vendors' models under their plain names, without a prefix. GWDG
    /// is the case that brought this up: next to open weights it resells Claude and GPT models.
    /// Blablador answers with a whole sentence instead of an ID.
    /// </summary>
    private static readonly CorpusEntry[] RESELLER_ENTRIES =
    [
        new(GWDG, "claude-sonnet-5", ON_THE_MANUAL_TEST_LIST),
        new(GWDG, "gpt-5.5", ON_THE_MANUAL_TEST_LIST),
        new(GWDG, "meta-llama-3.1-8b-instruct", NAMED_BY_A_RULE),
        new(GWDG, "qwen3-235b-a22b", NAMED_BY_A_RULE),
        new(GWDG, "deepseek-r1", NAMED_BY_A_RULE),
        new(GWDG, "gemma-3-27b-it", NAMED_BY_A_RULE),
        new(GWDG, "internvl2.5-8b", NAMED_BY_A_RULE),
        new(GWDG, "e5-mistral-7b-instruct", NAMED_BY_NO_RULE),
        new(GWDG, "whisper-large-v2", BUILT_INTO_THE_APP),
        new(HELMHOLTZ, "1 - Llama3 405 the best general model", QUOTED_AS_A_NAME_SHAPE),
        new(HELMHOLTZ, "01 - GPT-5.5 - great overall performance", QUOTED_AS_A_NAME_SHAPE),
        new(HELMHOLTZ, "10 - Muse Glimmer 30b - the newest META model", QUOTED_AS_A_NAME_SHAPE),
        new(HELMHOLTZ, "Qwen 3.8-27B with DFlash on haicluster", QUOTED_AS_A_NAME_SHAPE),
        new(HELMHOLTZ, "alias-qwen38-27b", QUOTED_AS_A_NAME_SHAPE),
        new(GROQ, "llama-3.3-70b-versatile", NAMED_BY_A_RULE),
        new(GROQ, "openai/gpt-oss-120b", NAMED_BY_A_RULE),
        new(GROQ, "moonshotai/kimi-k2-instruct", NAMED_BY_A_RULE),
        new(GROQ, "qwen/qwen3-32b", NAMED_BY_A_RULE),
        new(GROQ, "whisper-large-v3-turbo", NAMED_BY_NO_RULE),
        new(FIREWORKS, "accounts/fireworks/models/llama-v3p1-405b-instruct", QUOTED_AS_A_NAME_SHAPE),
        new(FIREWORKS, "accounts/fireworks/models/deepseek-v3", NAMED_BY_A_RULE),
        new(FIREWORKS, "accounts/fireworks/models/qwen3-235b-a22b", NAMED_BY_A_RULE),
        new(FIREWORKS, "whisper-v3", BUILT_INTO_THE_APP),
        new(HETZNER, "gpt-oss-120b", NAMED_BY_A_RULE),
        new(HETZNER, "qwen3-coder-30b", NAMED_BY_A_RULE),
        new(IONOS, "meta-llama/Llama-3.3-70B-Instruct", NAMED_BY_A_RULE),
        new(IONOS, "mistralai/Mistral-Small-24B-Instruct", NAMED_BY_A_RULE),
    ];

    /// <summary>
    /// Self-hosted engines. Ollama writes the variant behind a colon, which normalization turns
    /// into a hyphen, so a rolling tag such as "qwen3.8:latest" carries no size at all. This is the
    /// longest section on purpose: it is where the open-weight families arrive.
    /// </summary>
    private static readonly CorpusEntry[] SELF_HOSTED_ENTRIES =
    [
        new(SELF_HOSTED, "qwen3.8:latest", QUOTED_AS_A_NAME_SHAPE),
        new(SELF_HOSTED, "qwen3.8-2.4t-a95b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "qwen3.8:27b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "qwen3.5:32b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "qwen3.6:32b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "qwen3-coder:30b", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "qwen2.5-vl-7b-instruct", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "qwen3.8:27b-mlx", ON_THE_MANUAL_TEST_LIST),
        new(SELF_HOSTED, "qwq:32b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "deepseek-r1:32b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "deepseek-r1-distill-llama-70b", ON_THE_MANUAL_TEST_LIST),
        new(SELF_HOSTED, "deepseek-v3.1:671b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "deepseek-v2.5", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "llama3.2:3b", QUOTED_AS_A_NAME_SHAPE),
        new(SELF_HOSTED, "llama3.2-vision:11b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "llama2:13b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "llama-3.1-405b-base", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "muse-glimmer-30b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "gemma4:e2b", ON_THE_MANUAL_TEST_LIST),
        new(SELF_HOSTED, "gemma4:31b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "gemma3:1b", ON_THE_MANUAL_TEST_LIST),
        new(SELF_HOSTED, "gemma3:27b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "gemma3n:e4b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "gemma2:9b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "gpt-oss:20b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "mistral-small3.2:24b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "mistral-small-3.1-24b-instruct", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "mistral-nemo:12b", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "magistral:24b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "voxtral-mini-3b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "ministral-8b-instruct-2410", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "glm-5.3-flash-nvfp4", QUOTED_AS_A_NAME_SHAPE),
        new(SELF_HOSTED, "glm-5-2", QUOTED_AS_A_NAME_SHAPE),
        new(SELF_HOSTED, "glm-4.5v", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "glm-4-9b-chat", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "glm-4.6:latest", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "kimi-k3:latest", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "kimi-k2.7-code", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "kimi-vl:16b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "kimi-k2:1t", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "hunyuan:7b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "tencent/hy3", QUOTED_AS_A_NAME_SHAPE),
        new(SELF_HOSTED, "nemotron-3-49b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "nvidia-nemotron-3.5-lightning-30b-a3b-nvfp4", QUOTED_AS_A_NAME_SHAPE),
        new(SELF_HOSTED, "llama-3.3-nemotron-super-49b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "granite4.2:8b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "granite3.3:8b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "granite3.2-vision:2b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "granite-embedding:278m", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "command-a:111b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "command-a-plus", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "command-a-vision", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "command-a-reasoning", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "command-r7b:7b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "aya-expanse:8b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "aya-vision:8b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "olmo3:7b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "olmo-3-32b-think", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "olmo2:13b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "seed-oss:36b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "falcon-h1:7b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "falcon-h1-1.5b-tool-calling", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "falcon3:10b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "ling-1t", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "ring-1t", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "inclusionai/ling-mini-2.0", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "starling-lm:7b", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "ernie-4.5-vl-28b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "ernie-x1.1-thinking", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "ernie-4.5-21b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "smollm3:3b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "smollm2:1.7b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "apriel-1.5-15b-thinker", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "apriel-1.6-15b-thinker", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "internvl3-8b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "internlm3:8b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "apertus-1.5-8b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "phi4-mini:latest", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "phi-4-multimodal-instruct", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "phi-4-mini-reasoning", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "phi-4-reasoning-vision", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "phi4:14b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "phi3:14b", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "minimax-m2:latest", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "minimax-text-01", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "teuken-7b-instruct", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "eurollm-9b-instruct", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "occiglot-7b-eu5", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "salamandra-7b-instruct", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "salamandra-7b-instruct-tools", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "yi-1.5:9b", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "01-ai/yi-large", NAMED_BY_A_RULE),
        new(SELF_HOSTED, "nomic-embed-text:latest", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "a-model-nobody-has-heard-of", NAMED_BY_NO_RULE),
    ];

    /// <summary>
    /// Names that say nothing, and one provider which answers about nothing. They are here because
    /// a rebuild is exactly where a fresh crash on an empty string gets introduced.
    /// </summary>
    private static readonly CorpusEntry[] EDGE_CASE_ENTRIES =
    [
        new(OPEN_AI, "", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "   ", NAMED_BY_NO_RULE),
        new(SELF_HOSTED, "---", NAMED_BY_NO_RULE),
        new(NONE, "gpt-5.6", NAMED_BY_NO_RULE),
    ];

    /// <summary>
    /// Every entry of the corpus, in the order the sections above are written.
    /// </summary>
    /// <remarks>
    /// This has to stand below the sections it reads: static fields are initialized top to bottom,
    /// and a field which is not initialized yet is null rather than an error.
    /// </remarks>
    public static readonly IReadOnlyList<CorpusEntry> ENTRIES =
    [
        ..OPEN_AI_ENTRIES,
        ..ANTHROPIC_ENTRIES,
        ..GOOGLE_ENTRIES,
        ..MISTRAL_ENTRIES,
        ..ALIBABA_ENTRIES,
        ..DEEP_SEEK_ENTRIES,
        ..PERPLEXITY_ENTRIES,
        ..XAI_ENTRIES,
        ..GATEWAY_ENTRIES,
        ..HUGGING_FACE_ENTRIES,
        ..RESELLER_ENTRIES,
        ..SELF_HOSTED_ENTRIES,
        ..EDGE_CASE_ENTRIES,
    ];
}