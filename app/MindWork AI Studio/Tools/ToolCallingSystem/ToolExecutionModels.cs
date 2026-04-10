using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Settings;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutionContext
{
    public required ToolDefinition Definition { get; init; }

    public required SettingsManager SettingsManager { get; init; }

    public required IReadOnlyDictionary<string, string> SettingsValues { get; init; }
}

public sealed class ToolExecutionResult
{
    public string? TextContent { get; init; }

    public JsonNode? JsonContent { get; init; }

    public string ToModelContent()
    {
        if (this.JsonContent is not null)
            return this.JsonContent.ToJsonString();

        return this.TextContent ?? string.Empty;
    }
}

public enum ToolInvocationTraceStatus
{
    NONE = 0,
    SUCCESS,
    ERROR,
    BLOCKED,
}

public sealed class ToolInvocationTrace
{
    public int Order { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string ToolIcon { get; set; } = Icons.Material.Filled.Build;

    public string ToolCallId { get; set; } = string.Empty;

    public ToolInvocationTraceStatus Status { get; set; } = ToolInvocationTraceStatus.NONE;

    public bool WasExecuted { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public Dictionary<string, string> Arguments { get; set; } = [];

    public string Result { get; set; } = string.Empty;
}

public sealed class ToolRuntimeStatus
{
    public bool IsRunning { get; set; }

    public List<string> ToolNames { get; set; } = [];

    public string Message => this.ToolNames.Count switch
    {
        0 => string.Empty,
        1 => $"Using tool: {this.ToolNames[0]}",
        _ => $"Using tools: {string.Join(", ", this.ToolNames)}",
    };
}

public sealed class ToolConfigurationState
{
    public bool IsConfigured { get; init; }

    public List<string> MissingRequiredFields { get; init; } = [];
}

public sealed class ToolCatalogItem
{
    public required ToolDefinition Definition { get; init; }

    public required IToolImplementation Implementation { get; init; }

    public required ToolConfigurationState ConfigurationState { get; init; }
}

public sealed class ToolSelectionState
{
    public HashSet<string> SelectedToolIds { get; init; } = [];
}
