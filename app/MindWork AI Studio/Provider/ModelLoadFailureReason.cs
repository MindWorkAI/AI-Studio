namespace AIStudio.Provider;

public enum ModelLoadFailureReason
{
    NONE,
    INVALID_OR_MISSING_API_KEY,

    /// <summary>
    /// The API key contains a character which an HTTP header cannot carry, so the models were never requested.
    /// </summary>
    INVALID_API_KEY_CHARACTERS,

    AUTHENTICATION_OR_PERMISSION_ERROR,
    INSUFFICIENT_QUOTA,
    TOO_MANY_REQUESTS,
    PROVIDER_UNAVAILABLE,
    INVALID_RESPONSE,
    UNKNOWN,
}