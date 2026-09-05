using System.Diagnostics;
using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutor(ToolSettingsService toolSettingsService, ILogger<ToolExecutor> logger)
{
    private const string INVALID_TOOL_CALL_ERROR = "The tool call was invalid.";

    public (string Content, ToolInvocationTrace Trace, ConfidenceLevel RequiredProviderConfidence, IReadOnlyList<Source> Sources) CreateInvalidToolCallResult(
        string toolCallId,
        int order)
    {
        logger.LogWarning(
            "Rejected invalid tool call. ToolCallId={ToolCallId}, Order={Order}, Status={Status}",
            toolCallId,
            order,
            ToolInvocationTraceStatus.ERROR);
        return (INVALID_TOOL_CALL_ERROR, new ToolInvocationTrace
        {
            Order = order,
            ToolName = "Invalid tool call",
            ToolCallId = toolCallId,
            Status = ToolInvocationTraceStatus.ERROR,
            StatusMessage = INVALID_TOOL_CALL_ERROR,
            Result = INVALID_TOOL_CALL_ERROR,
        }, ConfidenceLevel.NONE, []);
    }

    public static bool IsValidArgumentsJson(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            return document.RootElement.ValueKind is JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task<(string Content, ToolInvocationTrace Trace, ConfidenceLevel RequiredProviderConfidence, IReadOnlyList<Source> Sources)> ExecuteAsync(
        string toolCallId,
        string toolName,
        string argumentsJson,
        IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools,
        IProvider provider,
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
        catch (JsonException)
        {
            //
            // Only the trace loses its arguments here; the execution below parses the same JSON
            // again and reports a broken call properly. The message says which call it was, but
            // nothing about its content: arguments may carry secrets, and a parser message quotes
            // the text it stumbled over.
            //
            logger.LogWarning("Could not read the arguments of a tool call for its trace. ToolName={ToolName}, ToolCallId={ToolCallId}", toolName, toolCallId);
        }

        logger.LogInformation(
            "Starting tool execution. ToolName={ToolName}, ToolCallId={ToolCallId}",
            toolName,
            toolCallId);
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
            }, ConfidenceLevel.NONE, []);
        }

        var definition = runnableTool.Definition;
        var implementation = runnableTool.Implementation;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var settingsValues = await toolSettingsService.GetSettingsAsync(definition);
            var settingsManager = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
            var result = await implementation.ExecuteAsync(document.RootElement, new ToolExecutionContext
            {
                Definition = definition,
                ToolCallId = toolCallId,
                SettingsManager = settingsManager,
                SettingsValues = settingsValues,
                ProviderConfidence = provider.Provider.GetConfidence(settingsManager).Level,
                ProviderIsTrustedByConfiguration = provider.IsTrustedByConfiguration(settingsManager),
            }, token);
            logger.LogInformation("Completed tool execution. ToolName={ToolName}, ToolCallId={ToolCallId}, DurationMs={DurationMs}, Status={Status}", toolName, toolCallId, stopwatch.ElapsedMilliseconds, ToolInvocationTraceStatus.SUCCESS);

            var resultModelContent = result.ToModelContent();
            var toolInvocationTrace = new ToolInvocationTrace
            {
                Order = order,
                ToolId = definition.Id,
                ToolName = implementation.GetDisplayName(),
                ToolIcon = implementation.Icon,
                ToolCallId = toolCallId,
                Status = ToolInvocationTraceStatus.SUCCESS,
                WasExecuted = true,
                Arguments = FormatArguments(document.RootElement,
                    implementation.SensitiveTraceArgumentNames),
                Result = result.TextContent ?? string.Empty,
                JsonResult = result.JsonContent,
            };

            return (resultModelContent, toolInvocationTrace, result.RequiredProviderConfidence, result.Sources);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogInformation("Completed tool execution. ToolName={ToolName}, ToolCallId={ToolCallId}, DurationMs={DurationMs}, Status={Status}", toolName, toolCallId, stopwatch.ElapsedMilliseconds, "CANCELED");
            throw;
        }
        catch (ToolExecutionBlockedException exception)
        {
            logger.LogWarning("Tool execution was blocked. ToolName={ToolName}, ToolCallId={ToolCallId}, DurationMs={DurationMs}, Status={Status}, Reason={Reason}", toolName, toolCallId, stopwatch.ElapsedMilliseconds, ToolInvocationTraceStatus.BLOCKED, exception.Message);

            var toolInvocationTrace = new ToolInvocationTrace
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
            };

            return (exception.Message, toolInvocationTrace, ConfidenceLevel.NONE, []);
        }
        catch (Exception exception)
        {
            var error = $"Tool execution failed: {exception.Message}";
            logger.LogError(exception, "Tool execution failed. ToolName={ToolName}, ToolCallId={ToolCallId}, DurationMs={DurationMs}, Status={Status}", toolName, toolCallId, stopwatch.ElapsedMilliseconds, ToolInvocationTraceStatus.ERROR);

            var toolInvocationTrace = new ToolInvocationTrace
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
            };

            return (error, toolInvocationTrace, ConfidenceLevel.NONE, []);
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
