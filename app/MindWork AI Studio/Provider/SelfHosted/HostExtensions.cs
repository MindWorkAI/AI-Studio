namespace AIStudio.Provider.SelfHosted;

public static class HostExtensions
{
    public static string Name(this Host host) => host switch
    {
        Host.NONE => "None",

        Host.LM_STUDIO => "LM Studio",
        Host.LLAMACPP => "llama.cpp",
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
    
    public static bool AreEmbeddingsSupported(this Host host)
    {
        switch (host)
        {
            case Host.LM_STUDIO:
            case Host.OLLAMA:
            case Host.VLLM:
                return true;
            
            default:
            case Host.LLAMACPP:
                return false;
        }
    }
}