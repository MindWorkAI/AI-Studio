using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// Whether the mail tools have anything to work with.
/// </summary>
internal static class MailToolConfiguration
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MailToolConfiguration).Namespace, nameof(MailToolConfiguration));

    /// <summary>
    /// The state the selection shows for a mail tool: not set up while there is no mailbox at all.
    /// </summary>
    /// <remarks>
    /// Whether the provider of a chat may read the mailboxes is no question here. That depends on
    /// the chat, so only the preparation of a request can answer it.
    /// </remarks>
    /// <param name="settingsManager">The settings, which hold the mailboxes.</param>
    /// <returns>Null when there is a mailbox, otherwise the state with what to do about it.</returns>
    public static ToolConfigurationState? GetState(SettingsManager settingsManager) => settingsManager.ConfigurationData.Mailboxes.Count > 0
        ? null
        : new ToolConfigurationState
        {
            IsConfigured = false,
            Message = TB("To use this tool, add a mailbox to your data sources first."),
        };
}