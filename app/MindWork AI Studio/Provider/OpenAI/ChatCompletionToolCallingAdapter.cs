using System.Text.Json;

using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.Harness;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Speaks the Chat Completions wire format for the tool calling loop.
/// </summary>
/// <remarks>
/// Tool calls arrive in the assistant message, and results go back as tool messages correlated by
/// tool call ID. Every OpenAI-compatible provider uses this shape.
/// </remarks>
public sealed class ChatCompletionToolCallingAdapter<TRequest>(
    Func<TextMessage, IDictionary<string, object>, IList<object>?, Task<TRequest>> requestFactory,
    TextMessage systemPrompt, IDictionary<string, object> apiParameters,
    IList<object> providerTools,
    IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools,
    Func<ChatCompletionAPIRequest, CancellationToken, Task<ChatCompletionResponse?>> executeRequestAsync,
    string providerInstanceName, ILogger logger)
    : IToolCallingProviderAdapter where TRequest : ChatCompletionAPIRequest
{
    private readonly List<IMessageBase> internalMessages = [];
    private ChatCompletionResponseMessage? lastResponseMessage;
    private List<ChatCompletionToolCall> lastToolCalls = [];

    /// <inheritdoc />
    public async Task<ToolCallingRound?> ExecuteRoundAsync(string? finalResponseInstruction, bool includeTools, CancellationToken token = default)
    {
        var requestSystemPrompt = finalResponseInstruction is null
            ? systemPrompt : systemPrompt with
            {
                Content = $"{systemPrompt.Content}{Environment.NewLine}{Environment.NewLine}{finalResponseInstruction}",
            };

        ChatCompletionAPIRequest requestDtoBase = await requestFactory(requestSystemPrompt, apiParameters, includeTools ? providerTools : null);
        var requestDto = requestDtoBase with
        {
            Messages = [..requestDtoBase.Messages, ..this.internalMessages],
            Stream = false,

            //
            // AI Studio runs tool calls one after another, so asking for parallel calls would
            // only produce work it then has to serialize anyway. Requests without tools omit the
            // parameter because some providers reject it then.
            //
            ParallelToolCalls = requestDtoBase.Tools is null ? null : false,
        };

        var response = await executeRequestAsync(requestDto, token);
        if (response is null)
            return null;

        var responseChoice = response.Choices?.FirstOrDefault();
        if (responseChoice?.Message is null)
        {
            logger.LogError(
                "The tool calling response did not contain a usable choice. ProviderInstanceName={ProviderInstanceName}, ChoiceCount={ChoiceCount}",
                providerInstanceName,
                response.Choices?.Count ?? 0);

            throw ToolCallingMessages.InvalidToolCallingResponse(providerInstanceName);
        }

        this.lastResponseMessage = responseChoice.Message;
        var preparedCalls = this.PrepareToolCalls(responseChoice.Message.ToolCalls ?? []);
        this.lastToolCalls = preparedCalls.Select(x => x.ToolCall).ToList();

        return new ToolCallingRound(
            responseChoice.Message.Content ?? string.Empty,
            preparedCalls
                .Select(x => new ToolCallingRequestedCall(x.ToolCall.Id!, x.ToolCall.Function!.Name!, x.ToolCall.Function!.Arguments!, x.IsValid))
                .ToList(),
            []);
    }

    /// <inheritdoc />
    public void RecordAssistantTurn() => this.internalMessages.Add(new AssistantToolCallMessage
    {
        Content = this.lastResponseMessage?.RawContent,
        ReasoningContent = this.lastResponseMessage?.ReasoningContent,
        ToolCalls = this.lastToolCalls,
    });

    /// <inheritdoc />
    /// <remarks>
    /// Chat Completions has no error flag on a tool message, so a failure travels in the content
    /// like any other result.
    /// </remarks>
    public void RecordToolResult(string callId, string content, bool isError = false) => this.internalMessages.Add(new ToolResultMessage
    {
        Content = content,
        ToolCallId = callId,
    });

    /// <summary>
    /// Normalizes the tool calls of one response.
    /// </summary>
    /// <remarks>
    /// Models get this wrong in several ways: a missing call ID, a missing function name, or
    /// arguments that are not valid JSON. None of that may reach a tool, but none of it may be
    /// dropped either — a call the model never hears about again leaves it waiting. So each call
    /// is either marked invalid and answered with an error, or corrected where that is safe.
    /// </remarks>
    private List<PreparedChatCompletionToolCall> PrepareToolCalls(IEnumerable<ChatCompletionToolCall?> toolCalls)
    {
        var preparedToolCalls = new List<PreparedChatCompletionToolCall>();
        foreach (var returnedToolCall in toolCalls)
        {
            //
            // Unlike the Responses API, Chat Completions does not need the ID to come from the
            // provider: it only has to match between our request and our answer. So a missing one
            // can be supplied instead of failing the call.
            //
            var toolCallId = string.IsNullOrWhiteSpace(returnedToolCall?.Id)
                ? $"call_{Guid.NewGuid():N}"
                : returnedToolCall.Id;

            var returnedFunctionName = returnedToolCall?.Function?.Name;
            var returnedArguments = returnedToolCall?.Function?.Arguments;
            var isValid = returnedToolCall?.Function is not null &&
                          !string.IsNullOrWhiteSpace(returnedFunctionName) &&
                          ToolExecutor.IsValidArgumentsJson(returnedArguments);

            var normalizedToolCall = new ChatCompletionToolCall
            {
                Id = toolCallId,
                Type = string.IsNullOrWhiteSpace(returnedToolCall?.Type) ? "function" : returnedToolCall.Type,
                AdditionalMetadata = returnedToolCall?.AdditionalMetadata ?? new Dictionary<string, JsonElement>(),
                Function = new ChatCompletionToolFunction
                {
                    Name = string.IsNullOrWhiteSpace(returnedFunctionName) ? "invalid_tool_call" : returnedFunctionName,
                    Arguments = returnedArguments ?? "{}",
                },
            };

            if (!isValid)
            {
                logger.LogWarning("Received an invalid Chat Completions tool call. ToolCallId={ToolCallId}", toolCallId);
                preparedToolCalls.Add(new PreparedChatCompletionToolCall(normalizedToolCall, false));
                continue;
            }

            var canonicalName = runnableTools
                .Select(x => x.Definition.Function.Name)
                .FirstOrDefault(x => x.Equals(returnedFunctionName!.Trim(), StringComparison.Ordinal));

            if (canonicalName is not null && !canonicalName.Equals(returnedFunctionName, StringComparison.Ordinal))
            {
                logger.LogWarning("Canonicalized tool call function name '{ReturnedFunctionName}' to '{CanonicalFunctionName}'.", returnedFunctionName, canonicalName);
                normalizedToolCall = normalizedToolCall with
                {
                    Function = normalizedToolCall.Function! with
                    {
                        Name = canonicalName,
                    },
                };
            }

            preparedToolCalls.Add(new PreparedChatCompletionToolCall(normalizedToolCall, true));
        }

        return preparedToolCalls;
    }

    private readonly record struct PreparedChatCompletionToolCall(ChatCompletionToolCall ToolCall, bool IsValid);
}