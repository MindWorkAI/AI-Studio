using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolRuntimeStatus
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ToolRuntimeStatus).Namespace, nameof(ToolRuntimeStatus));

    public bool IsRunning { get; set; }

    public List<string> ToolNames { get; set; } = [];

    public string Message => this.ToolNames.Count switch
    {
        0 => string.Empty,
        1 => string.Format(TB("Using tool: {0}"), this.ToolNames[0]),
        _ => string.Format(TB("Using tools: {0}"), string.Join(", ", this.ToolNames)),
    };
}
