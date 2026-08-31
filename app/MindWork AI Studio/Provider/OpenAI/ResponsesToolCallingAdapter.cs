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
    Func<ResponsesAPIRequest, CancellationToken, Task<ResponsesResponse?>> executeRequestAsync) : IToolCallingProviderAdapter
{
    private readonly List<object> internalItems = [];
    private ResponsesResponse? lastResponse;

    /// <summary>
    /// The tools offered to the model: the provider-native ones plus our local functions.
    /// </summary>
    /// <remarks>
    /// A provider-native tool whose type collides with one of our function names is dropped
    /// because the model could not tell the two apart.
    /// </remarks>
    private readonly IList<object> effectiveProviderTools = BuildEffectiveProviderTools(providerTools, runnableTools);

    /// <inheritdoc />
    public async Task<ToolCallingRound?> ExecuteRoundAsync(string? finalResponseInstruction, bool includeTools, CancellationToken token = default)
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

        var response = await executeRequestAsync(new ResponsesAPIRequest
        {
            Model = chatModel.Id,
            Input = requestInput,
            Stream = false,
            Store = false,
            Tools = includeTools ? this.effectiveProviderTools : [],
            AdditionalApiParameters = apiParameters,
        }, token);

        if (response is null)
            return null;

        this.lastResponse = response;
        return new ToolCallingRound(
            response.GetTextOutput(),
            response.GetFunctionCalls()
                .Select(call => new ToolCallingRequestedCall(
                    call.CallId ?? string.Empty,
                    call.Name ?? string.Empty,
                    call.Arguments ?? string.Empty,
                    !string.IsNullOrWhiteSpace(call.Name) && ToolExecutor.IsValidArgumentsJson(call.Arguments)))
                .ToList(),
            
            response.GetSources());
    }

    /// <inheritdoc />
    public void RecordAssistantTurn()
    {
        if (this.lastResponse is null)
            return;

        // Every output item, not just the function calls: the API rejects a continuation whose
        // reasoning items are missing.
        foreach (var outputItem in this.lastResponse.Output)
            this.internalItems.Add(outputItem);
    }

    /// <inheritdoc />
    public void RecordToolResult(string callId, string content) => this.internalItems.Add(new ResponsesFunctionCallOutputItem
    {
        CallId = callId,
        Output = content,
    });

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