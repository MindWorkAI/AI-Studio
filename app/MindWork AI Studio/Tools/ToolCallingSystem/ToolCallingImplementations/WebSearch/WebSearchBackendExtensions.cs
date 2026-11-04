namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

public static class WebSearchBackendExtensions
{
    extension(WebSearchBackend backend)
    {
        /// <summary>
        /// The name of one search service, as the user reads it and as a search result reports it.
        /// </summary>
        /// <remarks>
        /// Product names, so they are not translated: SearXNG is called SearXNG in every language.
        /// What gets stored is the enum member name instead, which is what leaves this free to be
        /// worded for people — in the settings dropdown, in a note explaining which service
        /// answered, and in the result the model reads.
        /// </remarks>
        public string ToName() => backend switch
        {
            WebSearchBackend.SEARXNG => "SearXNG",
            WebSearchBackend.STAAN => "Staan",
            WebSearchBackend.TAVILY => "Tavily",

            _ => backend.ToString(),
        };
    }
}