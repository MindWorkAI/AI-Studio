namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// Supplies tool definitions to the registry.
/// </summary>
/// <remarks>
/// Where a tool comes from and what a tool is are two different questions. AI Studio's own tools
/// are written in C#, plugin authors will describe theirs in Lua, and the assistants are to be
/// offered as tools as well — each arrives differently, yet the registry validates and serves
/// them all the same way.<br/><br/>
/// A source is asked once while the registry is being built. Definitions do not change while the
/// app runs; a plugin that was loaded later needs the registry rebuilt, not the source re-read.
/// </remarks>
public interface IToolDefinitionSource
{
    /// <summary>
    /// A name for this source, used in log messages about the definitions it produced.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// The definitions this source knows.
    /// </summary>
    /// <remarks>
    /// May return definitions the registry then rejects. Validating them is the registry's job,
    /// so that every source is held to the same rules — including the ones written by plugin
    /// authors, whose definitions AI Studio does not control.
    /// </remarks>
    public IEnumerable<ToolDefinition> GetDefinitions();
}