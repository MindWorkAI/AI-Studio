using System.Diagnostics;
using System.Text.Json;

using AIStudio.Provider;

using Microsoft.Extensions.DependencyInjection;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutor(ToolSettingsService toolSettingsService, ILogger<ToolExecutor> logger)
{
    public async Task<(string Content, ToolInvocationTrace Trace)> ExecuteAsync(
        string toolCallId,
        string toolName,
        string argumentsJson,
        IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools,
        ConfidenceLevel providerConfidence,
        int order,
        CancellationToken token = default)
    {
        var runnableTool = runnableTools.FirstOrDefault(x => x.Definition.Function.Name.Equals(toolName, StringComparison.Ordinal));
        Dictionary<string, string> formattedArguments = [];
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            formattedArguments = FormatArguments(document.RootElement, runnableTool.Implementation?.SensitiveTraceArgumentNames ?? EmptySensitiveTraceArgumentNames.INSTANCE);
        }
        catch
        {
        }

        logger.LogInformation(
            "Starting tool execution. ToolName={ToolName}, ToolCallId={ToolCallId}, ArgumentNames={ArgumentNames}",
            toolName,
            toolCallId,
            formattedArguments.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList());
        var stopwatch = Stopwatch.StartNew();
        if (runnableTool.Definition is null || runnableTool.Implementation is null)
        {
            var error = this.CreateError(toolName);
            logger.LogWarning("Completed tool execution. ToolName={ToolName}, ToolCallId={ToolCallId}, DurationMs={DurationMs}, Status={Status}", toolName, toolCallId, stopwatch.ElapsedMilliseconds, ToolInvocationTraceStatus.BLOCKED);
            return (error, new ToolInvocationTrace
            {
                Order = order,
                ToolId = toolName,
                ToolName = toolName,
                ToolCallId = toolCallId,
                Status = ToolInvocationTraceStatus.BLOCKED,
                StatusMessage = "Tool is not available in the current context.",
                Arguments = formattedArguments,
                Result = error,
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
                ProviderConfidence = providerConfidence,
            }, token);
            logger.LogInformation("Completed tool execution. ToolName={ToolName}, ToolCallId={ToolCallId}, DurationMs={DurationMs}, Status={Status}", toolName, toolCallId, stopwatch.ElapsedMilliseconds, ToolInvocationTraceStatus.SUCCESS);

            return (result.ToModelContent(), new ToolInvocationTrace
            {
                Order = order,
                ToolId = definition.Id,
                ToolName = implementation.GetDisplayName(),
                ToolIcon = implementation.Icon,
                ToolCallId = toolCallId,
                Status = ToolInvocationTraceStatus.SUCCESS,
                WasExecuted = true,
                Arguments = FormatArguments(document.RootElement, implementation.SensitiveTraceArgumentNames),
                Result = implementation.FormatTraceResult(result.ToModelContent()),
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (ToolExecutionBlockedException exception)
        {
            logger.LogWarning(exception, "Tool execution was blocked. ToolName={ToolName}, ToolCallId={ToolCallId}, DurationMs={DurationMs}, Status={Status}, ErrorMessage={ErrorMessage}", toolName, toolCallId, stopwatch.ElapsedMilliseconds, ToolInvocationTraceStatus.BLOCKED, exception.Message);

            return (exception.Message, new ToolInvocationTrace
            {
                Order = order,
                ToolId = definition.Id,
                ToolName = implementation.GetDisplayName(),
                ToolIcon = implementation.Icon,
                ToolCallId = toolCallId,
                Status = ToolInvocationTraceStatus.BLOCKED,
                StatusMessage = exception.Message,
                Arguments = formattedArguments,
                Result = exception.Message,
            });
        }
        catch (Exception exception)
        {
            var error = $"Tool execution failed: {exception.Message}";
            logger.LogError(exception, "Tool execution failed. ToolName={ToolName}, ToolCallId={ToolCallId}, DurationMs={DurationMs}, Status={Status}, ErrorMessage={ErrorMessage}", toolName, toolCallId, stopwatch.ElapsedMilliseconds, ToolInvocationTraceStatus.ERROR, exception.Message);

            return (error, new ToolInvocationTrace
            {
                Order = order,
                ToolId = definition.Id,
                ToolName = implementation.GetDisplayName(),
                ToolIcon = implementation.Icon,
                ToolCallId = toolCallId,
                Status = ToolInvocationTraceStatus.ERROR,
                StatusMessage = error,
                Arguments = formattedArguments,
                Result = error,
            });
        }
    }

    private static class EmptySensitiveTraceArgumentNames
    {
        public static readonly IReadOnlySet<string> INSTANCE = new HashSet<string>(StringComparer.Ordinal);
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
