using System.Text.Json;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

public sealed class GetCurrentWeatherTool : IToolImplementation
{
    public string ImplementationKey => "get_current_weather";

    public string Icon => Icons.Material.Filled.Cloud;

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public string GetDisplayName() => I18N.I.T("Current Weather", typeof(GetCurrentWeatherTool).Namespace, nameof(GetCurrentWeatherTool));

    public string GetDescription() => I18N.I.T("Use this demo tool to retrieve the current weather for a given city and state. It is primarily meant to demonstrate tool calling and tool settings in AI Studio.", typeof(GetCurrentWeatherTool).Namespace, nameof(GetCurrentWeatherTool));

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "demoLabel" => I18N.I.T("Demo Label", typeof(GetCurrentWeatherTool).Namespace, nameof(GetCurrentWeatherTool)),
        _ => I18N.I.T(fieldDefinition.Title, typeof(GetCurrentWeatherTool).Namespace, nameof(GetCurrentWeatherTool)),
    };

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "demoLabel" => I18N.I.T("Required demo setting for validating tool settings in tests. It does not affect the weather result.", typeof(GetCurrentWeatherTool).Namespace, nameof(GetCurrentWeatherTool)),
        _ => I18N.I.T(fieldDefinition.Description, typeof(GetCurrentWeatherTool).Namespace, nameof(GetCurrentWeatherTool)),
    };

    public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default)
    {
        var city = arguments.TryGetProperty("city", out var cityValue) ? cityValue.GetString() ?? string.Empty : string.Empty;
        var state = arguments.TryGetProperty("state", out var stateValue) ? stateValue.GetString() ?? string.Empty : string.Empty;
        var unit = arguments.TryGetProperty("unit", out var unitValue) ? unitValue.GetString() ?? string.Empty : string.Empty;

        if (unit is not ("celsius" or "fahrenheit"))
            throw new ArgumentException($"Invalid unit '{unit}'.");

        return Task.FromResult(new ToolExecutionResult
        {
            TextContent = $"The weather in {city}, {state} is 85 degrees {unit}. It is partly cloudy with highs in the 90's.",
        });
    }
}
