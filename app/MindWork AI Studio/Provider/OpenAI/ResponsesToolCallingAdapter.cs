using System.Runtime.CompilerServices;

using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.Harness;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Speaks the OpenAI Responses wire format for the tool calling loop.
/// </summary>
/// <remarks>
/// Function calls arrive as output items and results go back as function call output items,
/// correlated by call ID. Unlike Chat Completions, the whole output of a round has to be sent
/// back for the next one, reasoning items included, or the API refuses to continue.
/// </remarks>
public sealed class ResponsesToolCallingAdapter(Model chatModel, IList<object> baseInput, IDictionary<string, object> apiParameters, IList<object> providerTools,
    IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools,
    Func<ResponsesAPIRequest, CancellationToken, IAsyncEnumerable<ServerSentEvent>> streamRequestAsync) : IToolCallingProviderAdapter
{
    private readonly List<object> internalItems = [];
    private readonly List<string> recordedRequestTexts = [];
    private ResponsesResponse? lastResponse;

    /// <inheritdoc />
    public IReadOnlyList<string> RecordedRequestTexts => this.recordedRequestTexts;

    /// <summary>
    /// The tools offered to the model: the provider-native ones plus our local functions.
    /// </summary>
    /// <remarks>
    /// A provider-native tool whose type collides with one of our function names is dropped
    /// because the model could not tell the two apart.
    /// </remarks>
    private readonly IList<object> effectiveProviderTools = BuildEffectiveProviderTools(providerTools, runnableTools);

    /// <inheritdoc />
    public async IAsyncEnumerable<ToolCallingStreamEvent> ExecuteRoundAsync(string? finalResponseInstruction, bool includeTools, [EnumeratorCancellation] CancellationToken token = default)
    {
        var requestInput = new List<object>(baseInput);
        if (finalResponseInstruction is not null && requestInput.FirstOrDefault() is TextMessage systemPrompt)
        {
            requestInput[0] = systemPrompt with
            {
                Content = $"{systemPrompt.Content}{Environment.NewLine}{Environment.NewLine}{finalResponseInstruction}",
            };
        }

        requestInput.AddRange(this.internalItems);

        var request = new ResponsesAPIRequest
        {
            Model = chatModel.Id,
            Input = requestInput,
            Stream = true,
            Store = false,
            Tools = includeTools ? this.effectiveProviderTools : [],
            AdditionalApiParameters = apiParameters,
        };

        //
        // The text goes out while it is being written, the round only once the stream closed it.
        // Sources travel with the text because the API announces them as it cites them. The usage
        // goes out with every round: which of them describes the conversation is the loop's
        // decision, which knows which round this is.
        //
        var accumulator = new ResponsesStreamAccumulator();
        await foreach (var serverSentEvent in streamRequestAsync(request, token))
        {
            var part = accumulator.Process(serverSentEvent);
            if (part.HasContent || part.Usage.IsKnown)
                yield return ToolCallingStreamEvent.TextDelta(new ContentStreamChunk(part.TextDelta, part.Sources, Usage: part.Usage));
        }

        var response = accumulator.Build();
        if (response is null)
            yield break;

        this.lastResponse = response;
        yield return ToolCallingStreamEvent.RoundCompleted(new ToolCallingRound(
            response.GetTextOutput(),
            response.GetFunctionCalls()
                .Select(call => new ToolCallingRequestedCall(
                    call.CallId ?? string.Empty,
                    call.Name ?? string.Empty,
                    call.Arguments ?? string.Empty,
                    !string.IsNullOrWhiteSpace(call.Name) && ToolExecutor.IsValidArgumentsJson(call.Arguments)))
                .ToList(),
            
            response.GetSources()));
    }

    /// <inheritdoc />
    public void RecordAssistantTurn()
    {
        if (this.lastResponse is null)
            return;

        // Every output item, not just the function calls: the API rejects a continuation whose
        // reasoning items are missing.
        foreach (var outputItem in this.lastResponse.Output)
        {
            this.internalItems.Add(outputItem);

            //
            // The item as it came in, because that is how it goes back out. Reading the text out
            // of it would mean knowing every item type the API has, including the ones it gains
            // later -- and a reasoning item nobody recognized would then cost nothing here while
            // costing its tokens on the wire.
            //
            this.recordedRequestTexts.Add(outputItem.GetRawText());
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The Responses API has no error flag on a function call output, so a failure travels in the
    /// output like any other result.
    /// </remarks>
    public void RecordToolResult(string callId, string content, bool isError = false)
    {
        this.internalItems.Add(new ResponsesFunctionCallOutputItem
        {
            CallId = callId,
            Output = content,
        });

        if (!string.IsNullOrWhiteSpace(content))
            this.recordedRequestTexts.Add(content);
    }

    private static IList<object> BuildEffectiveProviderTools(IList<object> providerTools, IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools)
    {
        var localFunctionNames = runnableTools
            .Select(x => x.Definition.Function.Name)
            .ToHashSet(StringComparer.Ordinal);

        return providerTools
            .Where(x => x is not ProviderTool providerTool || !localFunctionNames.Contains(providerTool.Type))
            .Concat(runnableTools.Select(x => (object)ProviderToolAdapters.ToResponsesTool(x.Definition)))
            .ToList();
    }
}