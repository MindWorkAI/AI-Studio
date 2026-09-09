namespace AIStudio.Provider;

public enum ProviderRequestFailureReason
{
    NONE,
    INSUFFICIENT_QUOTA,
    TOO_MANY_REQUESTS,

    /// <summary>
    /// The provider does not serve the requested model.
    /// </summary>
    /// <remarks>
    /// This applies to gateways which route to other providers: the model exists, but the one
    /// meant to answer for it does not offer it.
    /// </remarks>
    MODEL_NOT_SUPPORTED_BY_PROVIDER,

    /// <summary>
    /// No usable API key was available, or the provider rejected the one we sent.
    /// </summary>
    /// <remarks>
    /// Both cases lead to the same place for the user: the key stored for this provider is not
    /// one the provider works with, and the settings are where they fix it.
    /// </remarks>
    INVALID_OR_MISSING_API_KEY,

    /// <summary>
    /// The key was accepted, but the account is not allowed to do what we asked for.
    /// </summary>
    /// <remarks>
    /// Typical causes are a key without the required scope, a model the account has no access
    /// to, and providers which refuse requests from the user's region.
    /// </remarks>
    AUTHENTICATION_OR_PERMISSION_ERROR,

    /// <summary>
    /// The provider could not be reached, or said that it cannot serve requests right now.
    /// </summary>
    PROVIDER_UNAVAILABLE,

    /// <summary>
    /// The provider does not know the requested model at all.
    /// </summary>
    MODEL_NOT_FOUND,

    /// <summary>
    /// The text we sent was longer than the model accepts.
    /// </summary>
    CONTEXT_LENGTH_EXCEEDED,

    /// <summary>
    /// The provider cannot create embeddings at all.
    /// </summary>
    EMBEDDINGS_NOT_SUPPORTED,

    /// <summary>
    /// The provider answered successfully, but with something we were not able to read.
    /// </summary>
    INVALID_RESPONSE,

    /// <summary>
    /// The request failed and we were not able to tell why.
    /// </summary>
    /// <remarks>
    /// Deliberately without a user message of its own: what the provider itself said about the
    /// failure tells the user more than a sentence which says nothing.
    /// </remarks>
    UNKNOWN,
}