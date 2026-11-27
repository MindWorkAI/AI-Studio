using AIStudio.Provider;

namespace AIStudio.AIOperations;

/// <summary>
/// A task which AI Studio hands to a model on its own: one request in, one result out, without a loop.
/// </summary>
/// <remarks>
/// <para>
/// An operation is registered as a singleton and keeps no state between calls, so several callers may run
/// it at the same time, e.g., two chats with RAG. Everything a call needs travels with it: the request, and
/// in the AIOperationContext the provider which the caller chose for it.
/// </para>
/// <para>
/// Each operation also declares a static method GetProviderSettings(SettingsManager), which reads the
/// provider settings of the operation fresh on every call. It is static, so that a service can learn which
/// provider would run without an instance of the operation. AIOperationProviderResolver chooses the
/// provider from these settings; see the AGENTS.md of this folder.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">What the operation works on.</typeparam>
/// <typeparam name="TResult">What the operation returns.</typeparam>
public interface IAIOperation<in TRequest, TResult>
{
    /// <summary>
    /// The name of the operation in the log, in English.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The component the operation runs as.
    /// </summary>
    /// <remarks>
    /// Its requests name it, e.g., in the User-Agent for self-hosted servers. Without it, an operation
    /// working for an assistant would be counted as chat.
    /// </remarks>
    public Tools.Components Component { get; }

    /// <summary>
    /// The confidence level which the content of the request requires of a provider.
    /// </summary>
    /// <remarks>
    /// It comes from the request alone, e.g., from the data sources it carries or from the confidence a
    /// chat requires. The minimum confidence of the operation itself is a setting and not part of it.
    /// </remarks>
    /// <param name="request">The request to run.</param>
    /// <returns>The confidence level the content requires, NONE when it requires nothing.</returns>
    public ConfidenceLevel GetRequiredConfidence(TRequest request);

    /// <summary>
    /// Runs the operation for one request.
    /// </summary>
    /// <param name="request">The request to run.</param>
    /// <param name="context">What the call runs with, the provider above all.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public Task<TResult> RunAsync(TRequest request, AIOperationContext context, CancellationToken token = default);
}