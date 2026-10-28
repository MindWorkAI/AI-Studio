using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools.AssistantLaunch;

/// <summary>
/// Starts an assistant or a chat with content sent to it, such as the result of an assistant or a message of a chat.
/// </summary>
/// <remarks>
/// Every place which sends content goes through here, so that all of them offer the same destinations and
/// hand the content over the same way. The service lives once per window, because it navigates there.
/// </remarks>
/// <param name="settingsManager">The settings, which decide the assistants the user sees.</param>
/// <param name="assistantSessionService">The sessions of the assistants, which decide whether a destination is busy.</param>
/// <param name="navigationManager">The navigation of the window the user sends from.</param>
public sealed class AssistantLaunchService(SettingsManager settingsManager, AssistantSessionService assistantSessionService, NavigationManager navigationManager)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantLaunchService).Namespace, nameof(AssistantLaunchService));

    /// <summary>
    /// The assistants and the chat which a component may send content to, in the order of the menu.
    /// </summary>
    /// <param name="sender">The component which sends.</param>
    /// <returns>The destinations the user can choose from.</returns>
    public IReadOnlyList<Components> GetDestinationsFor(Components sender) => SelectDestinations(sender, this.IsVisible);

    /// <summary>
    /// Opens the destination with the text sent to it.
    /// </summary>
    /// <remarks>
    /// An assistant which is running already keeps its run: the user gets a warning and sees the running
    /// session, and the text is not handed over. Otherwise, what an assistant left from an earlier run
    /// gives way to the text, so that the text is not overwritten when the assistant restores that run.
    /// The destination takes the text once it opens, see TakeTextPayload.
    /// </remarks>
    /// <param name="destination">The assistant, or the chat for the field of the next message.</param>
    /// <param name="payload">The text to send.</param>
    public async Task LaunchAsync(Components destination, TextLaunchPayload payload)
    {
        var route = destination.GetData().Route;
        if (destination.HasSingleSessionSlot() && assistantSessionService.GetSnapshots().Any(snapshot => snapshot.IsActive && snapshot.Key.Component == destination))
        {
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.Apps, TB("This assistant is already running. AI Studio opens the running session instead.")));
            navigationManager.NavigateTo(route);
            return;
        }

        // Only components with a single session slot may be cleared as a group. The visual briefing
        // assistant keys its sessions per briefing, so clearing by component would discard the
        // status of every stored briefing instead of the one we are about to open.
        if (destination.HasSingleSessionSlot())
            await assistantSessionService.ClearInactiveSessionsForComponentAsync(destination);

        MessageBus.INSTANCE.DeferMessage(null, TextEventFor(destination), payload);
        navigationManager.NavigateTo(route);
    }

    /// <summary>
    /// Takes the text which was sent to a component, if any.
    /// </summary>
    /// <remarks>
    /// The text waits for its destination until the destination opens. Only the last one sent counts, and
    /// taking it removes it, so that it arrives once.
    /// </remarks>
    /// <param name="destination">The component which opens.</param>
    /// <returns>The text sent, or null when nothing was sent to the component.</returns>
    public static TextLaunchPayload? TakeTextPayload(Components destination) => destination.AllowSendTo()
        ? MessageBus.INSTANCE.TakeDeferredMessages<TextLaunchPayload>(TextEventFor(destination)).LastOrDefault()
        : null;

    /// <summary>
    /// The event a text for the given destination travels with.
    /// </summary>
    /// <remarks>
    /// A text sent to the chat goes into the field for the next message rather than starting a chat of its
    /// own, which is why the chat takes it with an event of its own.
    /// </remarks>
    /// <param name="destination">The assistant or the chat.</param>
    /// <returns>The event.</returns>
    internal static Event TextEventFor(Components destination) => destination is Components.CHAT
        ? Event.SEND_TO_CHAT_INPUT
        : destination.GetData().Event;

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
