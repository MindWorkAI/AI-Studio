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
                return true;
            
            default:
            case Host.LM_STUDIO:
            case Host.LLAMA_CPP:
            case Host.LLMMAN:
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