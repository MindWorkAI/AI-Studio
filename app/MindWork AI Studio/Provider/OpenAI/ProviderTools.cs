namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Known provider-side tools for LLM providers.
/// </summary>
/// <remarks>
/// Right now, only our OpenAI provider is using tools. Thus, this class is located in the
/// OpenAI namespace. In the future, when other providers also support tools, this class can
/// be moved into the provider namespace.
/// </remarks>
public static class ProviderTools
{
    public static readonly ProviderTool WEB_SEARCH = new("web_search");
}
