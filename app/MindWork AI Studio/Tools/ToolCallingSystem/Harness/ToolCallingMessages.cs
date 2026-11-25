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
    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(ToolCallingMessages).Namespace, nameof(ToolCallingMessages));

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ToolCallingMessages).Namespace, nameof(ToolCallingMessages));

    /// <summary>
    /// Says in place of the answer that the model stayed silent after a tool call.
    /// </summary>
    public static string NoAnswerAfterToolCall => TB("The model completed the tool call but did not return a final answer.");

    /// <summary>
    /// Says in place of the answer that the model stayed silent after it used up its tool calls.
    /// </summary>
    public static string NoAnswerAfterLimit => TB("The model did not return a final answer after completing the available tool calls.");

    /// <summary>
    /// Builds the exception for a response that cannot be used to continue.
    /// </summary>
    /// <param name="providerInstanceName">The provider instance the user configured.</param>
    public static ProviderRequestException InvalidToolCallingResponse(string providerInstanceName) => new(
        ProviderRequestFailureReason.NONE,
        string.Format(TA("The provider '{0}' returned an invalid tool calling response. Check the provider's tool calling configuration and see the logs for details."), providerInstanceName));

    /// <summary>
    /// Tells the user that a tool round could not be requested at all.
    /// </summary>
    /// <remarks>
    /// Shared by every adapter: the status code is what the user can act on, and the wording
    /// should not differ by provider API.
    /// </remarks>
    /// <param name="statusCode">The status code the provider answered with.</param>
    public static async Task SendToolCallingRequestFailedAsync(int statusCode) => await MessageBus.INSTANCE.SendError(new(
        AppIcons.TOOL,
        string.Format(TA("The tool calling request failed with status code {0}. See the logs for details."), statusCode)));
}