using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutor(ToolSettingsService toolSettingsService)
{
    public async Task<(string Content, ToolInvocationTrace Trace)> ExecuteAsync(
        string toolCallId,
        string toolName,
        string argumentsJson,
        IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools,
        int order,
        CancellationToken token = default)
    {
        var runnableTool = runnableTools.FirstOrDefault(x => x.Definition.Function.Name.Equals(toolName, StringComparison.Ordinal));
        if (runnableTool.Definition is null || runnableTool.Implementation is null)
        {
            return (this.CreateError(toolName), new ToolInvocationTrace
            {
                Order = order,
                ToolId = toolName,
                ToolName = toolName,
                ToolCallId = toolCallId,
                Status = ToolInvocationTraceStatus.BLOCKED,
                StatusMessage = "Tool is not available in the current context.",
                Result = this.CreateError(toolName),
            });
        }

        var definition = runnableTool.Definition;
        var implementation = runnableTool.Implementation;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var settingsValues = await toolSettingsService.GetSettingsAsync(definition);
            var result = await implementation.ExecuteAsync(document.RootElement, new ToolExecutionContext
            {
                Definition = definition,
                SettingsManager = Program.SERVICE_PROVIDER.GetRequiredService<Settings.SettingsManager>(),
                SettingsValues = settingsValues,
            }, token);

            return (result.ToModelContent(), new ToolInvocationTrace
            {
                Order = order,
                ToolId = definition.Id,
                ToolName = definition.DisplayName,
                ToolIcon = definition.Icon,
                ToolCallId = toolCallId,
                Status = ToolInvocationTraceStatus.SUCCESS,
                WasExecuted = true,
                Arguments = FormatArguments(document.RootElement, implementation.SensitiveTraceArgumentNames),
                Result = implementation.FormatTraceResult(result.ToModelContent()),
            });
        }
        catch (Exception exception)
        {
            var error = $"Tool execution failed: {exception.Message}";
            Dictionary<string, string> formattedArguments = [];
            try
            {
                using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
                formattedArguments = FormatArguments(document.RootElement, implementation.SensitiveTraceArgumentNames);
            }
            catch
            {
            }

            return (error, new ToolInvocationTrace
            {
                Order = order,
                ToolId = definition.Id,
                ToolName = definition.DisplayName,
                ToolIcon = definition.Icon,
                ToolCallId = toolCallId,
                Status = ToolInvocationTraceStatus.ERROR,
                StatusMessage = error,
                Arguments = formattedArguments,
                Result = error,
            });
        }
    }

    private string CreateError(string toolName) => $"Tool '{toolName}' is not available.";

    private static Dictionary<string, string> FormatArguments(JsonElement rootElement, IReadOnlySet<string> sensitiveNames)
    {
        if (rootElement.ValueKind is not JsonValueKind.Object)
            return [];

        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in rootElement.EnumerateObject())
        {
            arguments[property.Name] = sensitiveNames.Contains(property.Name)
                ? "*****"
                : property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    _ => property.Value.ToString(),
                };
        }

        return arguments;
    }
}
