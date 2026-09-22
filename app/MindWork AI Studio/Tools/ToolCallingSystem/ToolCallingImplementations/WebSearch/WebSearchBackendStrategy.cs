namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// How the web search tool decides which of the configured search services answers a search.
/// </summary>
/// <remarks>
/// Stored and configured by name, so a member must never be renamed: an organization
/// addresses these in its configuration, and a user has one of them saved as their chosen
/// strategy. The numbers behind the names are not persisted anywhere.<br/><br/>
/// With a single configured service all three come to the same thing, which is why the tool
/// hides the choice until a second one is configured.
/// </remarks>
public enum WebSearchBackendStrategy
{
    /// <summary>
    /// Ask one service after another, until one of them returns hits.
    /// </summary>
    FAILOVER,

    /// <summary>
    /// Ask every configured service at once and combine what they return.
    /// </summary>
    PARALLEL,

    /// <summary>
    /// Ask only the chosen service.
    /// </summary>
    SPECIFIC,
}