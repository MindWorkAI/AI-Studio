using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Tools.AIJobs;

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