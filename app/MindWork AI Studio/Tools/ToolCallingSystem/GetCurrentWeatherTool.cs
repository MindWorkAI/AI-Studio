using System.Text.Json;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class GetCurrentWeatherTool : IToolImplementation
{
    public string ImplementationKey => "get_current_weather";

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

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
