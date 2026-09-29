using System.Runtime.CompilerServices;

using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.Harness;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// Speaks the Anthropic messages wire format for the tool calling loop.
/// </summary>
/// <remarks>
/// Anthropic works in content blocks rather than in separate message kinds: the model's turn is
/// one assistant message whose blocks may mix text, thinking, and tool uses, and the results go
/// back as tool result blocks inside a single user message. That difference is what made this
/// provider hard to support before the loop and the wire format were separated — it is now the
/// only thing this class is about.
/// </remarks>
public sealed class AnthropicToolCallingAdapter(Model chatModel, IList<IMessageBase> baseMessages, string systemPrompt, int maxTokens,
    IDictionary<string, object> apiParameters, IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools,
    Func<ChatRequest, CancellationToken, IAsyncEnumerable<ServerSentEvent>> streamRequestAsync) : IToolCallingProviderAdapter
{
    private readonly List<IMessageBase> internalMessages = [];
    private readonly List<AnthropicToolResultContent> pendingToolResults = [];
    private readonly List<string> recordedRequestTexts = [];
    private readonly List<AnthropicTool> tools = runnableTools.Select(x => ProviderToolAdapters.ToAnthropicTool(x.Definition)).ToList();
    private AnthropicResponse? lastResponse;

    /// <inheritdoc />
    public IReadOnlyList<string> RecordedRequestTexts => this.recordedRequestTexts;

    /// <inheritdoc />
    public async IAsyncEnumerable<ToolCallingStreamEvent> ExecuteRoundAsync(string? finalResponseInstruction, bool includeTools, [EnumeratorCancellation] CancellationToken token = default)
    {
        //
        // The results of the previous round are flushed here rather than when they were recorded:
        // they all belong in one user message, and only now is it certain that no more are coming.
        //
        if (this.pendingToolResults.Count > 0)
        {
            this.internalMessages.Add(new AnthropicToolResultMessage([..this.pendingToolResults]));
            this.pendingToolResults.Clear();
        }

        var request = new ChatRequest
        {
            Model = chatModel.Id,
            Messages = [..baseMessages, ..this.internalMessages],
            System = finalResponseInstruction is null
                ? systemPrompt
                : $"{systemPrompt}{Environment.NewLine}{Environment.NewLine}{finalResponseInstruction}",

            MaxTokens = maxTokens,
            Stream = true,
            Tools = includeTools && this.tools.Count > 0 ? this.tools : null,
            AdditionalApiParameters = apiParameters,
        };

        //
        // The text goes out while it is being written; the blocks are put back together behind
        // it, because they have to return to the provider exactly as they arrived. The usage goes
        // out with every round: which of them describes the conversation is the loop's decision,
        // which knows which round this is.
        //
        var accumulator = new AnthropicMessageStreamAccumulator();
        await foreach (var serverSentEvent in streamRequestAsync(request, token))
        {
            var part = accumulator.Process(serverSentEvent);
            if (part.HasContent || part.Usage.IsKnown)
                yield return ToolCallingStreamEvent.TextDelta(new ContentStreamChunk(part.TextDelta, [], Usage: part.Usage));
        }

        var response = accumulator.Build();
        if (response is null)
            yield break;

        this.lastResponse = response;
        yield return ToolCallingStreamEvent.RoundCompleted(new ToolCallingRound(
            response.GetTextOutput(),
            response.GetToolUses()
                .Select(toolUse => new ToolCallingRequestedCall(
                    toolUse.Id,
                    toolUse.Name,
                    toolUse.Arguments,
                    ToolExecutor.IsValidArgumentsJson(toolUse.Arguments)))
                .ToList(),
            []));
    }

    /// <inheritdoc />
    public void RecordAssistantTurn()
    {
        if (this.lastResponse is null)
            return;

        //
        // The blocks go back exactly as they arrived. Thinking blocks in particular have to be
        // returned unchanged for the model to continue from them.
        //
        this.internalMessages.Add(new AnthropicMessage([..this.lastResponse.Content]));

        //
        // And they are counted exactly as they arrived, for the same reason: a thinking block is
        // sent back whole, so what it costs is what it says, not what we could read out of it.
        //
        foreach (var contentBlock in this.lastResponse.Content)
            this.recordedRequestTexts.Add(contentBlock.GetRawText());
    }

    /// <inheritdoc />
    public void RecordToolResult(string callId, string content, bool isError = false)
    {
        this.pendingToolResults.Add(new AnthropicToolResultContent
        {
            ToolUseId = callId,
            Content = content,
            IsError = isError,
        });

        //
        // Noted here rather than when the results are flushed into their message: the round they
        // belong to is over, and whoever asks in the meantime has to see what it cost.
        //
        if (!string.IsNullOrWhiteSpace(content))
            this.recordedRequestTexts.Add(content);
    }
}