using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutor(ToolSettingsService toolSettingsService, ILogger<ToolExecutor> logger)
{
    private const string INVALID_TOOL_CALL_ERROR = "The tool call was invalid.";

    /// <summary>
    /// What the trace keeps of an argument the tool marked as sensitive, see IToolImplementation.SensitiveTraceArgumentNames.
    /// </summary>
    private const string MASKED_ARGUMENT = "*****";

    /// <summary>
    /// How the arguments are read for running a tool: every name only once per object.
    /// </summary>
    /// <remarks>
    /// JSON allows a name twice, and readers disagree on which of the values counts. A tool reads one
    /// of them, while the check for addresses the model wrote into its call reads both, and a provider
    /// which receives the call back may read the other. A call which names an argument twice therefore
    /// does not run, and the message of the parser, which names the argument but quotes none of the
    /// values, goes back to the model. The trace reads the arguments without this rule, so that the
    /// user still sees what the model sent.
    /// </remarks>
    private static readonly JsonDocumentOptions EXECUTION_ARGUMENT_OPTIONS = new()
    {
        AllowDuplicateProperties = false,
    };

    public ToolCallOutcome CreateInvalidToolCallResult(string toolCallId, int order)
    {
        logger.LogWarning(
            "Rejected invalid tool call. ToolCallId={ToolCallId}, Order={Order}, Status={Status}",
            toolCallId,
            order,
            ToolInvocationTraceStatus.ERROR);
        return new ToolCallOutcome(INVALID_TOOL_CALL_ERROR, new ToolInvocationTrace
        {
            Order = order,
            ToolName = "Invalid tool call",
            ToolCallId = toolCallId,
            Status = ToolInvocationTraceStatus.ERROR,
            StatusMessage = INVALID_TOOL_CALL_ERROR,
            Result = INVALID_TOOL_CALL_ERROR,
        });
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

    /// <summary>
    /// Whether a function name consists of 1 to 64 ASCII letters, digits, underscores, or hyphens.
    /// </summary>
    /// <remarks>
    /// The rule OpenAI states for function names. Our own definitions have to meet it to be offered
    /// at all, and every name a model returned has to meet it to be sent back in the conversation:
    /// vLLM checks the calls of the history with exactly this rule when it serves a Mistral model,
    /// and it rejects the whole request over a single name which breaks it.
    /// </remarks>
    public static bool IsValidFunctionName(string? functionName) =>
        !string.IsNullOrWhiteSpace(functionName) &&
        functionName.Length <= 64 &&
        functionName.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    public async Task<ToolCallOutcome> ExecuteAsync(
        string toolCallId,
        string toolName,
        string argumentsJson,
        IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools,
        IProvider provider,
        ChatThread chatThread,
        int order,
        CancellationToken token = default)
    {
        var runnableTool = runnableTools.FirstOrDefault(x => x.Definition.Function.Name.Equals(toolName, StringComparison.Ordinal));
        Dictionary<string, string> formattedArguments = [];
        var traceArgumentsJson = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var sensitiveNames = runnableTool.Implementation?.SensitiveTraceArgumentNames ?? EmptySensitiveTraceArgumentNames.INSTANCE;
            formattedArguments = FormatArguments(document.RootElement, sensitiveNames);
            traceArgumentsJson = WriteTraceArgumentsJson(document.RootElement, sensitiveNames);
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
            return new ToolCallOutcome(error, new ToolInvocationTrace
            {
                Order = order,
                ToolId = toolName,
                ToolName = toolName,
                ToolCallId = toolCallId,
                Status = ToolInvocationTraceStatus.BLOCKED,
                StatusMessage = "Tool is not available in the current context.",
                Arguments = formattedArguments,
                ArgumentsJson = traceArgumentsJson,
                Result = error,
            });
        }

        var definition = runnableTool.Definition;
        var implementation = runnableTool.Implementation;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson, EXECUTION_ARGUMENT_OPTIONS);
            var settingsManager = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();

            //
            // Asked again here, although the request only offers what was allowed: the tools of a
            // request are chosen once, before its first round, and a tool which reads a mailbox may
            // restrict the chat in the middle of it. The model can then still call a tool it was
            // offered, with the mail content in its arguments.
            //
            var outboundDataRestriction = chatThread.RequiredOutboundDataRestriction;
            if (!ToolSelectionRules.IsOutboundDataAllowed(outboundDataRestriction.Restriction, implementation))
                throw new ToolExecutionBlockedException(outboundDataRestriction.GetToolBlockedMessage(settingsManager.ConfigurationData));

            var settingsValues = await toolSettingsService.GetSettingsAsync(definition);
            var result = await implementation.ExecuteAsync(document.RootElement, new ToolExecutionContext
            {
                Definition = definition,
                ChatThread = chatThread,
                Provider = provider,
                ToolCallId = toolCallId,
                SettingsManager = settingsManager,
                SettingsValues = settingsValues,
                ProviderConfidence = provider.Provider.GetConfidence(settingsManager).Level,
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
                ArgumentsJson = WriteTraceArgumentsJson(document.RootElement, implementation.SensitiveTraceArgumentNames),
                Result = result.TextContent ?? string.Empty,
                JsonResult = result.JsonContent,
            };

            return new ToolCallOutcome(resultModelContent, toolInvocationTrace)
            {
                RequiredProviderConfidence = result.RequiredProviderConfidence,
                RequiredDataSecurity = result.RequiredDataSecurity,
                RequiredOutboundDataRestriction = result.RequiredOutboundDataRestriction,
                Sources = result.Sources,
                Passages = result.Passages,
                ReturnedWebAddresses = FindReturnedWebAddresses(result, document.RootElement),
            };
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
                ArgumentsJson = traceArgumentsJson,
                Result = exception.Message,
            };

            return new ToolCallOutcome(exception.Message, toolInvocationTrace);
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
                ArgumentsJson = traceArgumentsJson,
                Result = error,
            };

            return new ToolCallOutcome(error, toolInvocationTrace);
        }
    }

    private static class EmptySensitiveTraceArgumentNames
    {
        public static readonly IReadOnlySet<string> INSTANCE = new HashSet<string>(StringComparer.Ordinal);
    }

    private string CreateError(string toolName) => $"Tool '{toolName}' is not available.";

    /// <summary>
    /// The web addresses in a result which did not come from the model.
    /// </summary>
    /// <remarks>
    /// An address which stands in one of the arguments, even as a part of one, is an echo of what
    /// the model wrote and is left out. Case does not matter for that, so a tool which writes the
    /// host in lower case does not slip one through, and neither does a tool which writes the same
    /// request in another encoding, since an address in the arguments also counts by its request
    /// key. The texts are compared as the JSON values read, so an escape in the JSON cannot hide an
    /// echo either.
    /// </remarks>
    private static HashSet<string> FindReturnedWebAddresses(ToolExecutionResult result, JsonElement arguments)
    {
        var argumentTexts = new List<string>();
        CollectStrings(arguments, argumentTexts);

        var argumentRequestKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var address in argumentTexts.SelectMany(WebAddresses.Find))
            if (WebAddresses.TryCreateRequestKey(address, out var requestKey))
                argumentRequestKeys.Add(requestKey);

        var resultTexts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.TextContent))
            resultTexts.Add(result.TextContent);

        CollectStrings(result.JsonContent, resultTexts);

        var requestKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var address in resultTexts.SelectMany(WebAddresses.Find))
        {
            if (argumentTexts.Any(argument => argument.Contains(address, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (WebAddresses.TryCreateRequestKey(address, out var requestKey) && !argumentRequestKeys.Contains(requestKey))
                requestKeys.Add(requestKey);
        }

        return requestKeys;
    }

    /// <summary>
    /// Collects every text value of a JSON value, also those inside arrays and objects, but no property names.
    /// </summary>
    /// <param name="element">The JSON value.</param>
    /// <param name="texts">Where the texts go.</param>
    internal static void CollectStrings(JsonElement element, List<string> texts)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                texts.Add(element.GetString() ?? string.Empty);
                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    CollectStrings(property.Value, texts);

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectStrings(item, texts);

                break;
        }
    }

    private static void CollectStrings(JsonNode? node, List<string> texts)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var property in jsonObject)
                    CollectStrings(property.Value, texts);

                break;

            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                    CollectStrings(item, texts);

                break;

            case JsonValue jsonValue when jsonValue.GetValueKind() is JsonValueKind.String:
                texts.Add(jsonValue.GetValue<string>());
                break;
        }
    }

    private static Dictionary<string, string> FormatArguments(JsonElement rootElement, IReadOnlySet<string> sensitiveNames)
    {
        if (rootElement.ValueKind is not JsonValueKind.Object)
            return [];

        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in rootElement.EnumerateObject())
        {
            arguments[property.Name] = sensitiveNames.Contains(property.Name)
                ? MASKED_ARGUMENT
                : property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    _ => property.Value.ToString(),
                };
        }

        return arguments;
    }

    /// <summary>
    /// The arguments of a call as one JSON object for its trace, without the values of the sensitive ones.
    /// </summary>
    /// <remarks>
    /// Each value is written as the model passed it, so that its JSON type survives, see
    /// ToolInvocationTrace.ArgumentsJson. A name which appears twice is written once: such a call
    /// never runs, see EXECUTION_ARGUMENT_OPTIONS, and only the calls which ran show their arguments
    /// to the model later on.
    /// </remarks>
    private static string WriteTraceArgumentsJson(JsonElement rootElement, IReadOnlySet<string> sensitiveNames)
    {
        if (rootElement.ValueKind is not JsonValueKind.Object)
            return string.Empty;

        var writtenNames = new HashSet<string>(StringComparer.Ordinal);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in rootElement.EnumerateObject())
            {
                if (!sensitiveNames.Contains(property.Name) && writtenNames.Add(property.Name))
                    property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
