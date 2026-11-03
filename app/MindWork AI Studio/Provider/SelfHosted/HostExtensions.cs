namespace AIStudio.Provider.SelfHosted;

public static class HostExtensions
{
    public static string Name(this Host host) => host switch
    {
        Host.NONE => "None",

        Host.LM_STUDIO => "LM Studio",
        Host.LLAMA_CPP => "llama.cpp",
        Host.WHISPER_CPP => "whisper.cpp",
        Host.OLLAMA => "ollama",
        Host.VLLM => "vLLM",
        Host.LLMMAN => "llmman",

        _ => "Unknown",
    };

    public static string BaseURL(this Host host) => host switch
    {
        _ => "/v1/",
    };

    public static string ChatURL(this Host host) => host switch
    {
        _ => "chat/completions",
    };
    
    public static string TranscriptionURL(this Host host) => host switch
    {
        _ => "audio/transcriptions",
    };
    
    public static string EmbeddingURL(this Host host) => host switch
    {
        _ => "embeddings",
    };
    
    /// <summary>
    /// The largest image file, in bytes, which the engine accepts in a chat request, where we know of one.
    /// </summary>
    /// <remarks>
    /// Like the limit of a cloud provider, see LLMProvidersExtensions.GetMaxImageSizeBytes, this
    /// belongs to the API of the engine, not to the model. What we actually send is asked through
    /// IImageSourceExtensions.MaxImageSizeBytesFor.
    /// </remarks>
    /// <param name="host">The engine.</param>
    /// <returns>The size limit of one image file, or null when the engine states none we know of.</returns>
    public static long? GetMaxImageSizeBytes(this Host host) => host switch
    {
        //
        // llmman answers a chat request larger than 2 MiB with 413: its route /v1/chat/completions
        // keeps the default limit of its web framework, read on 2026-10-22 at
        // https://github.com/llmmanorg/llmman/blob/main/src/cmd/serve.rs. An image of 1 MiB takes
        // about 1.33 MiB as base64, which leaves room for the text of the conversation:
        //
        Host.LLMMAN => 1_048_576,

        _ => null,
    };

    public static bool IsChatSupported(this Host host)
    {
        switch (host)
        {
            case Host.WHISPER_CPP:
                return false;
            
            default:
            case Host.OLLAMA:
            case Host.VLLM:
            case Host.LM_STUDIO:
            case Host.LLAMA_CPP:
            case Host.LLMMAN:
                return true;
        }
    }
    
    public static bool IsEmbeddingSupported(this Host host)
    {
        switch (host)
        {
            case Host.LM_STUDIO:
            case Host.OLLAMA:
            case Host.VLLM:
            case Host.LLMMAN:
                return true;
            
            default:
            case Host.LLAMA_CPP:
                return false;
        }
    }
    
    public static bool IsTranscriptionSupported(this Host host)
    {
        switch (host)
        {
            case Host.OLLAMA:
            case Host.VLLM:
            case Host.WHISPER_CPP:
            case Host.LLMMAN:
                return true;

            default:
            case Host.LM_STUDIO:
            case Host.LLAMA_CPP:
                return false;
        }
    }

    /// <summary>
    /// Whether the engine answers the models route with the models it serves.
    /// </summary>
    /// <remarks>
    /// This is the only place which says so. The provider asks it before it loads the models of a
    /// chat, embedding, or transcription list, and the dialogs ask it before they offer to reload
    /// them; a new engine therefore needs no more than its line here. whisper.cpp is not among
    /// them: it serves the one model it was started with and has no route to list it, so the
    /// provider offers that model under a fixed name instead. llama.cpp is, even though builds
    /// older than the route answer it with 404; the provider handles those builds itself.
    /// </remarks>
    /// <param name="host">The engine.</param>
    /// <returns>True when the models can be loaded from the engine; otherwise, false.</returns>
    public static bool IsModelListSupported(this Host host)
    {
        switch (host)
        {
            case Host.LLAMA_CPP:
            case Host.OLLAMA:
            case Host.LM_STUDIO:
            case Host.VLLM:
            case Host.LLMMAN:
                return true;

            default:
            case Host.NONE:
            case Host.WHISPER_CPP:
                return false;
        }
    }
}