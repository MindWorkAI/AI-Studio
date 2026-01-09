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
                return false;
        }
    }
}