namespace AIStudio.AIOperations;

/// <summary>
/// What the caller of an AI operation adds to the choice of its provider.
/// </summary>
public sealed record AIOperationProviderOptions
{
    /// <summary>
    /// The IDs of the providers to fall back on when the preferred provider of the operation cannot be used, in order.
    /// </summary>
    /// <remarks>
    /// An empty ID is allowed and stands for a fallback nobody named, e.g., an assistant without a provider.
    /// </remarks>
    public IReadOnlyList<string?> FallbackProviderIds { get; init; } = [];

    /// <summary>
    /// Whether the minimum confidence of the operation applies to the fallbacks as well.
    /// </summary>
    /// <remarks>
    /// Usually it does: a provider somebody picked for something else has to clear the bar of the operation,
    /// otherwise a provider ruled out for it would still see its content. A caller turns it off where the
    /// fallback saw the content anyway, e.g., the model of a chat which summarizes that very chat.
    /// </remarks>
    public bool OwnMinimumConfidenceAppliesToFallbacks { get; init; } = true;

    /// <summary>
    /// An optional check of the caller, applied to every configured candidate.
    /// </summary>
    /// <remarks>
    /// It returns NONE when the provider passes, and otherwise the issue to report, usually
    /// NOT_ALLOWED_FOR_CONTENT or CONTEXT_WINDOW_TOO_SMALL.
    /// </remarks>
    public Func<Settings.Provider, AIOperationProviderIssue>? CheckProvider { get; init; }
}
