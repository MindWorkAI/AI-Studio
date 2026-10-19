namespace AIStudio.Tools.UserAPIKeys;

/// <summary>
/// What a provider says about an API key the user entered.
/// </summary>
public enum UserAPIKeyCheckOutcome
{
    /// <summary>
    /// The provider listed its models with the key, so it accepts the key.
    /// </summary>
    ACCEPTED,

    /// <summary>
    /// The provider refused the key. AI Studio does not store it.
    /// </summary>
    REJECTED,

    /// <summary>
    /// Nothing tells whether the provider accepts the key, e.g., because the server was out of reach or the
    /// provider cannot list its models. AI Studio stores the key anyway.
    /// </summary>
    NOT_CHECKED
}