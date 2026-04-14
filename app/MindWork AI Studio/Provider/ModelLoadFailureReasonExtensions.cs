namespace AIStudio.Provider;

public static class ModelLoadFailureReasonExtensions
{
    public static string ToUserMessage(this ModelLoadFailureReason failureReason, Func<string, string> translate, string providerName) => failureReason switch
    {
        ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY => string.Format(translate("We could not load models from '{0}'. The API key is probably missing, invalid, or expired."), providerName),
        ModelLoadFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR => string.Format(translate("We could not load models from '{0}'. The account or API key does not have the required permissions."), providerName),
        ModelLoadFailureReason.PROVIDER_UNAVAILABLE => string.Format(translate("We could not load models from '{0}' because the provider is currently unavailable or could not be reached."), providerName),
        ModelLoadFailureReason.INVALID_RESPONSE => string.Format(translate("We could not load models from '{0}' because the provider returned an unexpected response."), providerName),
        ModelLoadFailureReason.UNKNOWN => string.Format(translate("We could not load models from '{0}' due to an unknown error."), providerName),
        
        _ => string.Empty,
    };
}