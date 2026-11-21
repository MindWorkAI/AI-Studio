using AIStudio.Provider;

namespace AIStudio.AIOperations;

/// <summary>
/// What is known about one provider which an AI operation might use.
/// </summary>
/// <remarks>
/// AIOperationProviderResolver gathers these facts from the settings, and AIOperationProviderChoice decides
/// on them alone. That keeps the decision apart from the settings and easy to test.
/// </remarks>
/// <param name="Provider">The configured provider; Provider.NONE when nobody named one or when it is missing.</param>
/// <param name="IsNamed">Whether somebody named a provider here at all.</param>
/// <param name="Exists">Whether the named provider is configured.</param>
/// <param name="IsProhibited">Whether the organization prohibited the provider.</param>
/// <param name="Confidence">The confidence level of the provider.</param>
/// <param name="CallerIssue">What the check of the caller found; NONE when the provider passed it or when there is no such check.</param>
public sealed record AIOperationProviderCandidate(Settings.Provider Provider, bool IsNamed, bool Exists, bool IsProhibited, ConfidenceLevel Confidence, AIOperationProviderIssue CallerIssue)
{
    /// <summary>
    /// A candidate which nobody named.
    /// </summary>
    public static readonly AIOperationProviderCandidate NOT_NAMED = new(Settings.Provider.NONE, false, false, false, ConfidenceLevel.NONE, AIOperationProviderIssue.NONE);

    /// <summary>
    /// A candidate whose provider is no longer configured.
    /// </summary>
    public static readonly AIOperationProviderCandidate MISSING = new(Settings.Provider.NONE, true, false, false, ConfidenceLevel.NONE, AIOperationProviderIssue.NONE);
}