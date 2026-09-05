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
}