using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider;

public static class ModelLoadFailureReasonExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ModelLoadFailureReasonExtensions).Namespace, nameof(ModelLoadFailureReasonExtensions));

    public static string ToUserMessage(this ModelLoadFailureReason failureReason, string providerName) => failureReason switch
    {
        ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY => string.Format(TB("We could not load models from '{0}'. The API key is probably missing, invalid, or expired."), providerName),
        ModelLoadFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR => string.Format(TB("We could not load models from '{0}'. The account or API key does not have the required permissions."), providerName),
        ModelLoadFailureReason.PROVIDER_UNAVAILABLE => string.Format(TB("We could not load models from '{0}' because the provider is currently unavailable or could not be reached."), providerName),
        ModelLoadFailureReason.INVALID_RESPONSE => string.Format(TB("We could not load models from '{0}' because the provider returned an unexpected response."), providerName),
        ModelLoadFailureReason.UNKNOWN => string.Format(TB("We could not load models from '{0}' due to an unknown error."), providerName),
        
        _ => string.Empty,
    };
}