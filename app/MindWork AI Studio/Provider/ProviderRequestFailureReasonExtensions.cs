using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider;

public static class ProviderRequestFailureReasonExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ProviderRequestFailureReasonExtensions).Namespace, nameof(ProviderRequestFailureReasonExtensions));

    /// <summary>
    /// Names the kind of failure in a few words.
    /// </summary>
    /// <remarks>
    /// Meant as a label beside the full message, so a list of failures can be scanned instead of
    /// read: twenty entries which all say API key are one problem, not twenty.
    /// </remarks>
    public static string GetName(this ProviderRequestFailureReason failureReason) => failureReason switch
    {
        ProviderRequestFailureReason.INSUFFICIENT_QUOTA => TB("No credits left"),
        ProviderRequestFailureReason.TOO_MANY_REQUESTS => TB("Too many requests"),
        ProviderRequestFailureReason.MODEL_NOT_SUPPORTED_BY_PROVIDER => TB("Model not offered"),
        ProviderRequestFailureReason.INVALID_OR_MISSING_API_KEY => TB("API key problem"),
        ProviderRequestFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR => TB("Not permitted"),
        ProviderRequestFailureReason.PROVIDER_UNAVAILABLE => TB("Provider unreachable"),
        ProviderRequestFailureReason.MODEL_NOT_FOUND => TB("Model unknown"),
        ProviderRequestFailureReason.CONTEXT_LENGTH_EXCEEDED => TB("Text too long"),
        ProviderRequestFailureReason.EMBEDDINGS_NOT_SUPPORTED => TB("No embeddings"),
        ProviderRequestFailureReason.INVALID_RESPONSE => TB("Unreadable answer"),
        ProviderRequestFailureReason.UNKNOWN => TB("Unknown cause"),

        _ => string.Empty,
    };

    /// <summary>
    /// Gets a value indicating whether the way out of this failure is in the provider settings.
    /// </summary>
    /// <remarks>
    /// Only for the failures a setting actually fixes. Pointing at the settings for a provider
    /// which is merely overloaded would send the user looking for a mistake they never made.
    /// </remarks>
    public static bool IsFixedInProviderSettings(this ProviderRequestFailureReason failureReason) => failureReason is
        ProviderRequestFailureReason.INVALID_OR_MISSING_API_KEY or
        ProviderRequestFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR or
        ProviderRequestFailureReason.MODEL_NOT_FOUND or
        ProviderRequestFailureReason.MODEL_NOT_SUPPORTED_BY_PROVIDER or
        ProviderRequestFailureReason.EMBEDDINGS_NOT_SUPPORTED or
        ProviderRequestFailureReason.CONTEXT_LENGTH_EXCEEDED;
}