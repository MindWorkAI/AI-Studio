namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// Supplies the definitions of the tools written in C#.
/// </summary>
/// <remarks>
/// For a tool implemented in the app itself, the definition and the implementation are one
/// object: the implementation states what it is. That removes the string key that used to join a
/// definition file to its class, and with it the failure where a typo in that key made the tool
/// disappear with nothing but a warning in the log.
/// </remarks>
public sealed class CodeToolDefinitionSource(IEnumerable<IToolImplementation> implementations) : IToolDefinitionSource
{
    /// <inheritdoc />
    public string SourceName => "code";

    /// <inheritdoc />
    public IEnumerable<ToolDefinition> GetDefinitions() => implementations.Select(implementation => implementation.GetDefinition());
}