namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Known tools for LLM providers.
/// </summary>
/// <remarks>
/// Right now, only our OpenAI provider is using tools. Thus, this class is located in the
/// OpenAI namespace. In the future, when other providers also support tools, this class can
/// be moved into the provider namespace.
/// </remarks>
public static class Tools
{
    public static readonly Tool WEB_SEARCH = new("web_search");
}