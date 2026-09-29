namespace AIStudio.Provider.SelfHosted;

public enum Host
{
    NONE,

    LM_STUDIO,
    LLAMA_CPP,
    WHISPER_CPP,
    OLLAMA,
    VLLM,

    // llmman, local runner with OpenAI-compatible /v1/ routes: https://github.com/llmmanorg/llmman
    LLMMAN,
}