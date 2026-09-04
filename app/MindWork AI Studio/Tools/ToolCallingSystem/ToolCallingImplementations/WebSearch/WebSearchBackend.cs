namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// The search services the web search tool can ask.
/// </summary>
/// <remarks>
/// Stored and configured by name, so a member must never be renamed: an organization
/// addresses these in its configuration, and a user has one of them saved as their chosen
/// backend. The numbers behind the names are not persisted anywhere.
/// </remarks>
public enum WebSearchBackend
{
    SEARXNG,
}