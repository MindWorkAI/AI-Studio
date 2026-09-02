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

    /// <inheritdoc />
    public async IAsyncEnumerable<ContentStreamChunk> RunAsync(
        IToolCallingProviderAdapter adapter,
        ToolCallingLoopContext context,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var toolCallCount = 0;
        var toolResultCharacterCount = 0L;
        var toolSources = new List<Source>();

        while (true)
        {
            //
            // Both limits end the conversation the same way: the model is told that it has no
            // tools left and is asked for its best answer from what it already has.
            //
            var finalResponseInstruction = ToolSelectionRules.GetToolCallsUnavailableInstruction(toolCallCount, toolResultCharacterCount);
            var finalResponseRequired = finalResponseInstruction is not null;

            var round = await adapter.ExecuteRoundAsync(finalResponseInstruction, !finalResponseRequired, token);
            if (round is null)
            {
                await context.ResetToolRuntimeStatusAsync();
                yield break;
            }

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
                context.AddToolInvocation(unanswerableTrace);
                await context.ResetToolRuntimeStatusAsync();
                yield return new ContentStreamChunk(unanswerableContent, [..toolSources]);
                yield break;
            }

            if (finalResponseRequired)
            {
                await context.ResetToolRuntimeStatusAsync();
                yield return new ContentStreamChunk(
                    string.IsNullOrWhiteSpace(round.TextOutput) ? NO_ANSWER_AFTER_LIMIT : round.TextOutput,
                    [..toolSources]);

                yield break;
            }

            if (round.Calls.Count is 0)
            {
                await context.ResetToolRuntimeStatusAsync();
                if (!string.IsNullOrWhiteSpace(round.TextOutput))
                {
                    yield return new ContentStreamChunk(round.TextOutput, [..toolSources]);
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

                foreach (var call in round.Calls)
                {
                    if (!call.IsValid)
                    {
                        toolCallCount++;
                        var (invalidContent, invalidTrace, _, _) = context.ToolExecutor.CreateInvalidToolCallResult(call.CallId, toolCallCount);
                        toolResultCharacterCount += invalidContent.Length;
                        context.AddToolInvocation(invalidTrace);
                        adapter.RecordToolResult(call.CallId, invalidContent, isError: true);
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
                        continue;
                    }

                    toolCallCount++;
                    var (toolContent, trace, requiredProviderConfidence, sources) = await context.ToolExecutor.ExecuteAsync(
                        call.CallId,
                        call.ToolName,
                        call.ArgumentsJson,
                        context.RunnableTools,
                        context.Provider,
                        toolCallCount,
                        token);

                    toolResultCharacterCount += toolContent.Length;
                    context.ChatThread.RequireProviderConfidence(requiredProviderConfidence);
                    toolSources.MergeSources(sources);
                    context.AddToolInvocation(trace);

                    // A blocked call counts as a failure towards the model as much as an errored
                    // one does: in both cases it did not get the data it asked for.
                    adapter.RecordToolResult(call.CallId, toolContent, trace.Status is not ToolInvocationTraceStatus.SUCCESS);
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