namespace AIStudio.Provider;

public static class LLMProvidersIconExtensions
{
    private const string ICON_ROOT = "/images/provider-icons";
    private const string SVG_DATA_URL_PREFIX = "data:image/svg+xml;base64,";

    public static string GetIconUrl(this AIStudio.Settings.Provider provider, bool isDarkMode)
        => provider.UsedLLMProvider.GetIconUrl(isDarkMode, provider.CustomIconDataUrl);

    public static string GetIconUrl(this LLMProviders provider, bool isDarkMode, string? customIconDataUrl)
    {
        if (customIconDataUrl?.StartsWith(SVG_DATA_URL_PREFIX, StringComparison.Ordinal) == true)
            return customIconDataUrl;

        return provider.GetIconUrl(isDarkMode);
    }

    public static string GetIconUrl(this LLMProviders provider, bool isDarkMode) => provider switch
    {
        LLMProviders.NONE => $"{ICON_ROOT}/provider{DarkVariant(isDarkMode)}.svg",
        LLMProviders.OPEN_AI => $"{ICON_ROOT}/openai{DarkVariant(isDarkMode)}.svg",
        LLMProviders.ANTHROPIC => $"{ICON_ROOT}/anthropic{DarkVariant(isDarkMode)}.svg",
        LLMProviders.MISTRAL => $"{ICON_ROOT}/mistral.svg",
        LLMProviders.GOOGLE => $"{ICON_ROOT}/google.svg",
        LLMProviders.X => $"{ICON_ROOT}/x{DarkVariant(isDarkMode)}.svg",
        LLMProviders.DEEP_SEEK => $"{ICON_ROOT}/deepseek.svg",
        LLMProviders.ALIBABA_CLOUD => $"{ICON_ROOT}/alibaba-cloud.svg",
        LLMProviders.PERPLEXITY => $"{ICON_ROOT}/perplexity.svg",
        LLMProviders.OPEN_ROUTER => $"{ICON_ROOT}/openrouter.svg",
        LLMProviders.HETZNER => $"{ICON_ROOT}/hetzner.svg",
        LLMProviders.IONOS => $"{ICON_ROOT}/ionos.svg",
        LLMProviders.GROQ => $"{ICON_ROOT}/groq.svg",
        LLMProviders.FIREWORKS => $"{ICON_ROOT}/fireworks.svg",
        LLMProviders.HUGGINGFACE => $"{ICON_ROOT}/hugging-face.svg",
        LLMProviders.SELF_HOSTED => $"{ICON_ROOT}/self-hosted{DarkVariant(isDarkMode)}.svg",
        LLMProviders.HELMHOLTZ => $"{ICON_ROOT}/helmholtz.svg",
        LLMProviders.GWDG => $"{ICON_ROOT}/gwdg.svg",
        _ => $"{ICON_ROOT}/provider{DarkVariant(isDarkMode)}.svg",
    };

    private static string DarkVariant(bool isDarkMode) => isDarkMode ? "-dark" : string.Empty;
}
