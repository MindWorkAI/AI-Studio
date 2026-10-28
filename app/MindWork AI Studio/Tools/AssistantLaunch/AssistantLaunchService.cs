using AIStudio.Settings;

namespace AIStudio.Tools.AssistantLaunch;

/// <summary>
/// Starts an assistant or a chat with content sent to it, such as the result of an assistant or a message of a chat.
/// </summary>
/// <remarks>
/// Every place which sends content goes through here, so that all of them offer the same destinations. The service
/// lives once per window, because it belongs to what the user does there.
/// </remarks>
/// <param name="settingsManager">The settings, which decide the assistants the user sees.</param>
public sealed class AssistantLaunchService(SettingsManager settingsManager)
{
    /// <summary>
    /// The assistants and the chat which a component may send content to, in the order of the menu.
    /// </summary>
    /// <param name="sender">The component which sends.</param>
    /// <returns>The destinations the user can choose from.</returns>
    public IReadOnlyList<Components> GetDestinationsFor(Components sender) => SelectDestinations(sender, this.IsVisible);

    /// <summary>
    /// The destinations a component may send content to, see GetDestinationsFor.
    /// </summary>
    /// <remarks>
    /// An assistant never offers itself: the content would arrive in the very assistant it came from,
    /// and since its page is open already, it would not even show up there. A chat still offers a new
    /// chat, because that is another chat. Shorter names come first, as everywhere in the menu.
    /// </remarks>
    /// <param name="sender">The component which sends.</param>
    /// <param name="isVisible">Whether the user sees the given assistant at all.</param>
    /// <returns>The destinations the user can choose from.</returns>
    internal static IReadOnlyList<Components> SelectDestinations(Components sender, Func<Components, bool> isVisible) => Enum.GetValues<Components>()
        .Where(destination => destination.AllowSendTo())
        .Where(destination => destination != sender || destination is Components.CHAT)
        .Where(isVisible)
        .OrderBy(destination => destination.Name().Length)
        .ToArray();

    private bool IsVisible(Components destination) => settingsManager.IsAssistantVisible(destination, withLogging: false, requiredPreviewFeature: destination.RequiredPreviewFeature());
}
