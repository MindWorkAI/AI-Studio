using System.Runtime.CompilerServices;

using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem.Harness;

/// <summary>
/// The sequential tool calling loop: ask the model, run what it asked for, ask again.
/// </summary>
/// <remarks>
/// One implementation for every provider API. Everything that differs between Chat Completions,
/// the Responses API, and Anthropic's messages lives in the adapter, so adding a provider means
/// writing an adapter, not another loop.<br/><br/>
/// Tool calls run one after another. A tool may of course work concurrently inside itself, as the
/// web search does when it loads several pages.
/// </remarks>
public sealed class ToolCallingLoop(ILogger<ToolCallingLoop> logger) : IToolCallingLoop
{
    private const string NO_ANSWER_AFTER_TOOL_CALL = "The model completed the tool call but did not return a final answer.";
    private const string NO_ANSWER_AFTER_LIMIT = "The model did not return a final answer after completing the available tool calls.";
    
    /// <summary>
    /// What separates the text of one round from the text of the next one.
    /// </summary>
    /// <remarks>
    /// A model may write before it calls a tool and again after the result came back. Without a
    /// separator, the last word of one round and the first of the next would run into each other,
    /// since each round is a text of its own rather than a continuation.
    /// </remarks>
    private const string ROUND_TEXT_SEPARATOR = "\n\n";
    
    /// <inheritdoc />
    public async IAsyncEnumerable<ContentStreamChunk> RunAsync(
        IToolCallingProviderAdapter adapter,
        ToolCallingLoopContext context,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var toolCallCount = 0;
        var toolResultCharacterCount = 0L;
        var toolSources = new List<Source>();
        var hasStreamedTextBefore = false;
        var isFirstRound = true;

        while (true)
        {
            //
            // Both limits end the conversation the same way: the model is told that it has no
            // tools left and is asked for its best answer from what it already has.
            //
            var finalResponseInstruction = ToolSelectionRules.GetToolCallsUnavailableInstruction(toolCallCount, toolResultCharacterCount);
            var finalResponseRequired = finalResponseInstruction is not null;

            ToolCallingRound? round = null;
            var roundStreamedText = false;

            //
            // Only the first round passes on what its request cost. Its prompt is the conversation up
            // to the question, which is exactly what the next question will be sent after. Every later
            // round carries the tool calls and their results on top, and none of that is sent again
            // once the answer stands -- a report of such a round would count a chat far larger than
            // the one the next request carries. What the answer adds, all rounds of text together, is
            // counted from its text afterwards, cf. ReportedHistory.
            //
            // Decided here rather than in the adapters, because every wire format reports its usage
            // per request, and which request this is only the loop knows for all of them alike.
            //
            var passesOnUsage = isFirstRound;
            isFirstRound = false;

            //
            // The model's words go out while the round is still running. That includes what it
            // writes before a tool call -- "let me look that up" -- which used to be dropped on
            // the floor because only the round's outcome was ever shown.
            //
            await foreach (var streamEvent in adapter.ExecuteRoundAsync(finalResponseInstruction, !finalResponseRequired, token))
            {
                if (streamEvent.Kind is ToolCallingStreamEventKind.ROUND_COMPLETED)
                {
                    round = streamEvent.Round;
                    continue;
                }
                
                if (streamEvent.Delta is null)
                    continue;

                var delta = streamEvent.Delta;
                if (!passesOnUsage && delta.Usage.IsKnown)
                {
                    // A delta which carried nothing but the usage has nothing left to hand over:
                    if (delta.Content.Length is 0 && delta.Sources.Count is 0)
                        continue;

                    delta = delta with { Usage = TokenUsage.UNKNOWN };
                }

                if (!string.IsNullOrWhiteSpace(delta.Content))
                {
                    //
                    // The separator goes out once the new round actually has something to say:
                    // otherwise it would trail a round which only called a tool.
                    //
                    if (!roundStreamedText && hasStreamedTextBefore)
                        yield return new ContentStreamChunk(ROUND_TEXT_SEPARATOR, []);

                    roundStreamedText = true;
                    hasStreamedTextBefore = true;
                }

                yield return delta;
            }
            
            //
            // No outcome means the round failed: the request errored out, or the stream ended
            // mid-sentence. Either way the adapter has already reported it.
            //
            if (round is null)
            {
                await context.ResetToolRuntimeStatusAsync();
                yield break;
            }
            
            var roundAnswered = roundStreamedText || !string.IsNullOrWhiteSpace(round.TextOutput);
            toolSources.MergeSources(round.Sources);

            //
            // A call without an ID cannot be answered: the provider correlates the result by that
            // ID, and inventing one would have the next request rejected. Nothing can be salvaged
            // from this round, so the conversation ends here.
            //
            if (round.Calls.Any(call => string.IsNullOrWhiteSpace(call.CallId)))
            {
                toolCallCount++;
                var (unanswerableContent, unanswerableTrace, _, _) = context.ToolExecutor.CreateInvalidToolCallResult(string.Empty, toolCallCount);
                await context.AddToolInvocationAsync(unanswerableTrace);
                await context.ResetToolRuntimeStatusAsync();
                yield return new ContentStreamChunk(unanswerableContent, [..toolSources]);
                yield break;
            }

            if (finalResponseRequired)
            {
                await context.ResetToolRuntimeStatusAsync();
                
                //
                // The answer itself is out already, so what is left to hand over are the sources
                // the tools contributed. An empty chunk is how sources travel on their own; the
                // streaming paths of the providers attach their annotations the same way.
                //
                yield return new ContentStreamChunk(
                    roundAnswered ? string.Empty : NO_ANSWER_AFTER_LIMIT,
                    [..toolSources]);

                yield break;
            }

            if (round.Calls.Count is 0)
            {
                await context.ResetToolRuntimeStatusAsync();
                if (roundAnswered)
                {
                    yield return new ContentStreamChunk(string.Empty, [..toolSources]);
                    yield break;
                }

                if (toolCallCount > 0)
                {
                    yield return new ContentStreamChunk(NO_ANSWER_AFTER_TOOL_CALL, [..toolSources]);
                    yield break;
                }

                //
                // Neither text nor a tool call on the very first round: there is nothing to show
                // and nothing to run. Staying silent would look like a hung request, so this is
                // reported as what it is — a provider that did not answer.
                //
                logger.LogError(
                    "The tool calling response contained neither text nor tool calls. ProviderInstanceName={ProviderInstanceName}, ProviderType={ProviderType}, ModelId={ModelId}",
                    context.ProviderInstanceName,
                    context.ProviderType,
                    context.ModelId);

                throw ToolCallingMessages.InvalidToolCallingResponse(context.ProviderInstanceName);
            }

            try
            {
                var validToolNames = round.Calls
                    .Where(call => call.IsValid)
                    .Select(call => GetDisplayName(context, call.ToolName))
                    .ToList();

                if (validToolNames.Count > 0)
                    await context.ShowToolRuntimeStatusAsync(validToolNames);

                // The model's turn has to be recorded before its results, or the provider sees
                // results for a turn it does not know about:
                adapter.RecordAssistantTurn();
                await context.PublishPendingToolConversationAsync(adapter);

                foreach (var call in round.Calls)
                {
                    if (!call.IsValid)
                    {
                        toolCallCount++;
                        var (invalidContent, invalidTrace, _, _) = context.ToolExecutor.CreateInvalidToolCallResult(call.CallId, toolCallCount);
                        toolResultCharacterCount += invalidContent.Length;
                        await context.AddToolInvocationAsync(invalidTrace);
                        adapter.RecordToolResult(call.CallId, invalidContent, isError: true);
                        await context.PublishPendingToolConversationAsync(adapter);
                        continue;
                    }

                    //
                    // The limits are checked again per call, because one round may ask for
                    // several tools and the earlier ones can exhaust the budget:
                    //
                    var callsUnavailableInstruction = ToolSelectionRules.GetToolCallsUnavailableInstruction(toolCallCount, toolResultCharacterCount);
                    if (callsUnavailableInstruction is not null)
                    {
                        adapter.RecordToolResult(call.CallId, callsUnavailableInstruction);
                        await context.PublishPendingToolConversationAsync(adapter);
                        continue;
                    }

                    toolCallCount++;
                    var (toolContent, trace, requiredProviderConfidence, requiredDataSecurity, sources) = await context.ToolExecutor.ExecuteAsync(
                        call.CallId,
                        call.ToolName,
                        call.ArgumentsJson,
                        context.RunnableTools,
                        context.Provider,
                        context.ChatThread,
                        toolCallCount,
                        token);

                    toolResultCharacterCount += toolContent.Length;
                    context.ChatThread.RequireProviderConfidence(requiredProviderConfidence);
                    context.ChatThread.RequireDataSecurity(requiredDataSecurity);
                    toolSources.MergeSources(sources);
                    await context.AddToolInvocationAsync(trace);

                    // A blocked call counts as a failure towards the model as much as an errored
                    // one does: in both cases it did not get the data it asked for.
                    adapter.RecordToolResult(call.CallId, toolContent, trace.Status is not ToolInvocationTraceStatus.SUCCESS);
                    await context.PublishPendingToolConversationAsync(adapter);
                }
            }
            finally
            {
                await context.ResetToolRuntimeStatusAsync();
            }
        }
    }

    private static string GetDisplayName(ToolCallingLoopContext context, string toolName) => context.RunnableTools
            .FirstOrDefault(tool => tool.Definition.Function.Name.Equals(toolName, StringComparison.Ordinal))
            .Implementation?.GetDisplayName() ?? toolName;
}