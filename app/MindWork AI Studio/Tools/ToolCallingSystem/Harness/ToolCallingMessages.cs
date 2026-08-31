using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.Harness;

/// <summary>
/// The messages the tool calling harness shows the user.
/// </summary>
/// <remarks>
/// Shared between the loop and its adapters: an unusable response looks the same to the user
/// whether the loop or the adapter noticed it, and one wording means one translation.
/// </remarks>
public static class ToolCallingMessages
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ToolCallingMessages).Namespace, nameof(ToolCallingMessages));

    /// <summary>
    /// Builds the exception for a response that cannot be used to continue.
    /// </summary>
    /// <param name="providerInstanceName">The provider instance the user configured.</param>
    public static ProviderRequestException InvalidToolCallingResponse(string providerInstanceName) => new(
        ProviderRequestFailureReason.NONE,
        string.Format(TB("The provider '{0}' returned an invalid tool calling response. Check the provider's tool calling configuration and see the logs for details."), providerInstanceName));
}