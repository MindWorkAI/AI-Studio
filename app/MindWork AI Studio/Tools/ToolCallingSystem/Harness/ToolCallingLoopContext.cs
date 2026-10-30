using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Tools.ToolCallingSystem.Harness;

/// <summary>
/// Everything one run of the tool calling loop needs besides its provider adapter.
/// </summary>
public sealed class ToolCallingLoopContext
{
    /// <summary>
    /// The chat the loop runs for. Tool results may raise its required provider confidence.
    /// </summary>
    public required ChatThread ChatThread { get; init; }

    /// <summary>
    /// The tools the model may call in this run.
    /// </summary>
    public required IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> RunnableTools { get; init; }

    public required ToolExecutor ToolExecutor { get; init; }

    /// <summary>
    /// The provider running the conversation, needed to judge what a tool may return to it.
    /// </summary>
    public required IProvider Provider { get; init; }

    /// <summary>
    /// The assistant message being built, or null when there is none to update.
    /// </summary>
    /// <remarks>
    /// The loop writes the tool traces and the live status into this instance, which is already
    /// part of the chat thread. That is how the UI learns about a running tool without the loop
    /// having to yield anything.
    /// </remarks>
    public ContentText? CurrentAssistantContent { get; init; }

    public required string ProviderInstanceName { get; init; }

    public required LLMProviders ProviderType { get; init; }

    public required string ModelId { get; init; }

    /// <summary>
    /// Whether the user may allow a tool for the rest of the chat, see ToolApprovalRules.MayApproveForWholeChat.
    /// </summary>
    /// <remarks>
    /// Read once per request. False unless the caller says otherwise, so a context which forgets it
    /// asks for every call rather than letting an allowance count which the organization forbade.
    /// </remarks>
    public bool MayApproveToolsForWholeChat { get; init; }

    /// <summary>
    /// How much the user wants to be asked, see ToolApprovalRules.GetMode. Read once per request.
    /// </summary>
    public ToolApprovalMode ToolApprovalMode { get; init; } = ToolApprovalMode.ASK_FOR_CRITICAL_TOOLS;

    /// <summary>
    /// Finds the collection of a tool, see ToolRegistry.GetCollectionId. An allowance for the chat holds for a whole collection.
    /// </summary>
    public Func<string, string> GetToolCollectionId { get; init; } = toolId => toolId;

    /// <summary>
    /// Asks the user to confirm a tool call, when it needs confirmation.
    /// </summary>
    /// <remarks>
    /// Waits for as long as the user takes, and ends with the token: pressing stop while a call
    /// waits stops the request. Nobody can answer when there is no assistant message to show the
    /// request on, or when nobody sees the thread, see ChatThread.RuntimeAsksForToolApproval. The
    /// call then runs only when its collection was allowed beforehand.
    /// </remarks>
    /// <param name="definition">The definition of the tool.</param>
    /// <param name="implementation">The implementation of the tool.</param>
    /// <param name="argumentsJson">The arguments the model passed.</param>
    /// <param name="token">The cancellation token of the request.</param>
    /// <returns>Whether and how the call may run.</returns>
    public async Task<ToolCallApproval> ApproveToolCallAsync(ToolDefinition definition, IToolImplementation implementation, string argumentsJson, CancellationToken token)
    {
        bool requiresApproval;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            requiresApproval = ToolApprovalRules.RequiresApproval(this.ToolApprovalMode, implementation, document.RootElement, this.ChatThread);
        }
        catch (JsonException)
        {
            // Arguments which cannot be read cannot run either. The executor reports them as broken:
            return ToolCallApproval.NOT_NEEDED;
        }

        if (!requiresApproval)
            return ToolCallApproval.NOT_NEEDED;

        var collectionId = this.GetToolCollectionId(definition.Id);
        if (this.MayApproveToolsForWholeChat && this.ChatThread.RuntimeApprovedToolCollectionIds.Contains(collectionId))
        {
            await this.ShowApprovedToolAsync(implementation, ToolApprovalDecision.RUN_ALWAYS_IN_THIS_CHAT);
            return ToolCallApproval.Allowed(ToolApprovalDecision.RUN_ALWAYS_IN_THIS_CHAT);
        }

        if (this.CurrentAssistantContent is null || !this.ChatThread.RuntimeAsksForToolApproval)
            return ToolCallApproval.NOBODY_TO_CONFIRM;

        var request = new ToolApprovalRequest
        {
            ToolName = implementation.GetDisplayName(),
            ToolIcon = implementation.Icon,
            Arguments = ToolExecutor.FormatTraceArguments(argumentsJson, implementation),
            MayApproveForWholeChat = this.MayApproveToolsForWholeChat,
        };

        ToolApprovalDecision decision;
        this.CurrentAssistantContent.PendingToolApproval = request;
        try
        {
            await this.AnnounceAsync(this.CurrentAssistantContent);
            await this.ReportWaitingForToolApprovalAsync(true);
            decision = await request.WaitAsync(token);
        }
        finally
        {
            this.CurrentAssistantContent.PendingToolApproval = null;
            await this.ReportWaitingForToolApprovalAsync(false);
            await this.AnnounceAsync(this.CurrentAssistantContent);
        }

        switch (decision)
        {
            case ToolApprovalDecision.RUN_ALWAYS_IN_THIS_CHAT:
                if (this.MayApproveToolsForWholeChat)
                    this.ChatThread.RuntimeApprovedToolCollectionIds.Add(collectionId);

                await this.ShowApprovedToolAsync(implementation, decision);
                return ToolCallApproval.Allowed(decision);

            case ToolApprovalDecision.RUN:
                await this.ShowApprovedToolAsync(implementation, decision);
                return ToolCallApproval.Allowed(decision);

            default:
                return ToolCallApproval.DECLINED_BY_USER;
        }
    }

    /// <summary>
    /// Tells whoever runs this thread that a tool call waits for confirmation, or no longer does.
    /// </summary>
    /// <remarks>
    /// A chat runs as a job, an assistant as a session, and both run on when the user leaves them.
    /// Each marks itself and notifies the user, so a confirmation never waits where nobody looks.
    /// Asked for here rather than taken as dependencies, like the job service in AnnounceAsync:
    /// only one of them knows the thread, and neither is around in every context.
    /// </remarks>
    private async Task ReportWaitingForToolApprovalAsync(bool isWaiting)
    {
        if (Program.SERVICE_PROVIDER.GetService<AIJobService>() is { } jobService)
            await jobService.SetWaitingForToolApprovalAsync(this.ChatThread.ChatId, isWaiting);

        if (Program.SERVICE_PROVIDER.GetService<AssistantSessionService>() is { } assistantSessionService)
            await assistantSessionService.SetWaitingForToolApprovalAsync(this.ChatThread.ChatId, isWaiting);
    }

    /// <summary>
    /// Tells the UI that a tool runs with the permission of the user, and how it got it.
    /// </summary>
    /// <remarks>
    /// Names only this tool, although the status of the round names all of its tools: the
    /// permission belongs to this call. The status is reset when the round ends, as always.
    /// </remarks>
    private async Task ShowApprovedToolAsync(IToolImplementation implementation, ToolApprovalDecision decision)
    {
        if (this.CurrentAssistantContent is null)
            return;

        this.CurrentAssistantContent.ToolRuntimeStatus = new ToolRuntimeStatus
        {
            IsRunning = true,
            ToolNames = [implementation.GetDisplayName()],
            Approval = decision,
        };

        await this.AnnounceAsync(this.CurrentAssistantContent);
    }

    /// <summary>
    /// Records one tool invocation for the UI.
    /// </summary>
    /// <remarks>
    /// Tells the UI right away, so a finished call shows up while the next one is still running.
    /// Waiting for the round to end would leave the user watching a list that lags behind what the
    /// model is doing.
    /// </remarks>
    public async Task AddToolInvocationAsync(ToolInvocationTrace trace)
    {
        if (this.CurrentAssistantContent is null)
            return;

        this.CurrentAssistantContent.ToolInvocations.Add(trace);
        await this.AnnounceAsync(this.CurrentAssistantContent);
    }

    /// <summary>
    /// Hands the conversation the adapter has accumulated to the assistant message.
    /// </summary>
    /// <remarks>
    /// Called after every recording, not once per round: a round which reads five web pages is the
    /// one during which the request grows the most, and a number which only moves between rounds
    /// would stand still through exactly that.
    /// </remarks>
    /// <param name="adapter">The adapter of this run, which knows what it has recorded.</param>
    public async Task PublishPendingToolConversationAsync(IToolCallingProviderAdapter adapter)
    {
        if (this.CurrentAssistantContent is null)
            return;

        this.CurrentAssistantContent.PendingToolConversation = [..adapter.RecordedRequestTexts];
        await this.AnnounceAsync(this.CurrentAssistantContent);
    }

    /// <summary>
    /// Tells the UI that the named tools are running.
    /// </summary>
    public async Task ShowToolRuntimeStatusAsync(IEnumerable<string> toolNames)
    {
        if (this.CurrentAssistantContent is null)
            return;

        this.CurrentAssistantContent.ToolRuntimeStatus = new ToolRuntimeStatus
        {
            IsRunning = true,
            ToolNames = [.. toolNames],
        };

        await this.AnnounceAsync(this.CurrentAssistantContent);
    }

    /// <summary>
    /// Clears the running-tool status.
    /// </summary>
    /// <remarks>
    /// Must happen on every path leaving a round, including the failing ones: a status left
    /// behind tells the user a tool is still running when nothing is.
    /// </remarks>
    public async Task ResetToolRuntimeStatusAsync()
    {
        if (this.CurrentAssistantContent is null)
            return;

        this.CurrentAssistantContent.ToolRuntimeStatus = new();
        await this.AnnounceAsync(this.CurrentAssistantContent);
    }

    /// <summary>
    /// Says that something about the running answer has changed.
    /// </summary>
    /// <remarks>
    /// Two receivers, because the screen is built from two of them. The content's own event
    /// renders the message block, which is what shows a running tool and the calls it has made.
    /// The job service renders the chat around it, and that is what recounts the tokens -- which
    /// nothing else would ask for during a tool run: the chat hears about progress one streamed
    /// chunk at a time, and a tool run produces none until it is over.<br/><br/>
    /// One method rather than two calls at each of the four places above, because the second of
    /// them is the one which is easy to forget.
    /// </remarks>
    /// <param name="content">The assistant message which changed.</param>
    private async Task AnnounceAsync(ContentText content)
    {
        await content.StreamingEvent();

        //
        // Asked for here rather than taken as a dependency: the same loop runs for the assistants,
        // where there is no job to tell and nothing which counts tokens.
        //
        var jobService = Program.SERVICE_PROVIDER.GetService<AIJobService>();
        if (jobService is not null)
            await jobService.NotifyChatActivityAsync(this.ChatThread.ChatId);
    }
}