using AIStudio.Chat;
using AIStudio.Provider;

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
    public void AddToolInvocation(ToolInvocationTrace trace) => this.CurrentAssistantContent?.ToolInvocations.Add(trace);

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

        await this.CurrentAssistantContent.StreamingEvent();
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
        await this.CurrentAssistantContent.StreamingEvent();
    }
}