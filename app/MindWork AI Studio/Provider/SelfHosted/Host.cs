namespace AIStudio.Provider.SelfHosted;

public enum Host
{
    NONE,

    LM_STUDIO,
    LLAMACPP,
    OLLAMA,
}

public static class HostExtensions
{
    public static string Name(this Host host) => host switch
    {
        Host.NONE => "None",

        Host.LM_STUDIO => "LM Studio",
        Host.LLAMACPP => "llama.cpp",
        Host.OLLAMA => "ollama",

        _ => "Unknown",
    };

    public static string BaseURL(this Host host) => host switch
    {
        Host.LM_STUDIO => "/v1/",
        Host.LLAMACPP => "/v1/",
        Host.OLLAMA => "/v1/",

        _ => "/v1/",
    };

    public static string ChatURL(this Host host) => host switch
    {
        Host.LM_STUDIO => "chat/completions",
        Host.LLAMACPP => "chat/completions",
        Host.OLLAMA => "chat/completions",

        _ => "chat/completions",
    };
}