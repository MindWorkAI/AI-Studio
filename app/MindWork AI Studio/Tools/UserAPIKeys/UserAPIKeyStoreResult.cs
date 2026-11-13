namespace AIStudio.Tools.UserAPIKeys;

/// <summary>
/// The result of storing the API key of the user for the providers of one configuration plugin.
/// </summary>
/// <param name="Check">What the provider said about the key. A rejected key was not stored at all.</param>
/// <param name="FailedProviderNames">The names of the providers for which the key could not be stored.</param>
public sealed record UserAPIKeyStoreResult(UserAPIKeyCheckOutcome Check, IReadOnlyList<string> FailedProviderNames)
{
    /// <summary>
    /// The result for a key which the provider refused, so that it was not stored.
    /// </summary>
    public static readonly UserAPIKeyStoreResult REJECTED = new(UserAPIKeyCheckOutcome.REJECTED, []);
}