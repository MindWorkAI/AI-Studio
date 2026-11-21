namespace AIStudio.AIOperations;

/// <summary>
/// Why a provider was not chosen for an AI operation.
/// </summary>
/// <remarks>
/// The members follow the order in which AIOperationProviderChoice checks a candidate, so that a candidate
/// reports the first rule it breaks.
/// </remarks>
public enum AIOperationProviderIssue
{
    /// <summary>
    /// Nothing stands in the way of the provider.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// Nobody named a provider here.
    /// </summary>
    /// <remarks>
    /// That is no problem to report: an operation without a provider of its own falls back as intended.
    /// </remarks>
    NOT_SET,

    /// <summary>
    /// The named provider is no longer configured, e.g., because the user deleted it.
    /// </summary>
    MISSING,

    /// <summary>
    /// The organization prohibited the provider.
    /// </summary>
    PROHIBITED,

    /// <summary>
    /// The confidence level of the provider is below the global minimum or below the minimum of the operation.
    /// </summary>
    BELOW_MINIMUM_CONFIDENCE,

    /// <summary>
    /// The content of the request must not reach the provider.
    /// </summary>
    /// <remarks>
    /// Either the confidence level of the provider is below what the content requires, or a check of the
    /// caller refused the provider, e.g., for the security of a data source or the restrictions of a chat.
    /// </remarks>
    NOT_ALLOWED_FOR_CONTENT,

    /// <summary>
    /// A check of the caller found the context window of the model too small for the operation.
    /// </summary>
    CONTEXT_WINDOW_TOO_SMALL,
}
