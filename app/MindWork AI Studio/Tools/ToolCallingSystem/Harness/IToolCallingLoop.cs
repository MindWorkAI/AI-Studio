using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem.Harness;

/// <summary>
/// Drives a conversation in which a model may call tools before it answers.
/// </summary>
/// <remarks>
/// Resolved through dependency injection so that a different harness can take over later without
/// touching the providers: an agent mode needs more than "ask, execute, ask again", but it speaks
/// to providers through the same adapters.
/// </remarks>
public interface IToolCallingLoop
{
    /// <summary>
    /// Runs the conversation until the model answers, the limits are reached, or the request fails.
    /// </summary>
    /// <param name="adapter">The adapter for the provider API in use.</param>
    /// <param name="context">The chat, tools, and UI state this run belongs to.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The model's final answer, with the sources the tools contributed.</returns>
    public IAsyncEnumerable<ContentStreamChunk> RunAsync(IToolCallingProviderAdapter adapter, ToolCallingLoopContext context, CancellationToken token = default);
}