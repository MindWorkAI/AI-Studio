using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Security;

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
/// <param name="promptInjectionGuardService">The filter for what an AI wrote, before it reaches another model.</param>
public sealed class AssistantLaunchService(SettingsManager settingsManager, AssistantSessionService assistantSessionService, NavigationManager navigationManager, PromptInjectionGuardService promptInjectionGuardService)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantLaunchService).Namespace, nameof(AssistantLaunchService));

    /// <summary>
    /// The assistants and the chat which a component may send content to, in the order of the menu.
    /// </summary>
    /// <param name="sender">The component which sends.</param>
    /// <returns>The destinations the user can choose from.</returns>
    public IReadOnlyList<Components> GetDestinationsFor(Components sender) => SelectDestinations(sender, this.IsVisible);

    /// <summary>
    /// Prepares a text for sending, together with the restrictions it takes along.
    /// </summary>
    /// <remarks>
    /// What an AI wrote goes through the prompt injection filter first: an answer may repeat what the AI
    /// read, such as a web page or a mail, and the instructions hidden there with it. Once sent, the text
    /// reaches the next model as a message of the user, which nobody filters. The user hears about what
    /// was removed right away, since each sending is an action of its own. The web addresses of the
    /// filtered text count as written by a model, so none of them counts as given by the user later
    /// on.<br/><br/>
    /// What the user wrote is sent as it is.
    /// </remarks>
    /// <param name="text">The text to send.</param>
    /// <param name="writtenByModel">Whether an AI wrote the text, rather than the user.</param>
    /// <param name="sourceRestrictions">The restrictions of where the text comes from.</param>
    /// <param name="sourceName">The name of the chat or of the assistant the text comes from, for the report of the filter.</param>
    /// <returns>The text to send, with its restrictions.</returns>
    public async Task<TextLaunchPayload> CreateTextPayloadAsync(string text, bool writtenByModel, ChatRestrictions sourceRestrictions, string sourceName)
    {
        if (!writtenByModel)
            return new(text, sourceRestrictions);

        var filteredText = await promptInjectionGuardService.SanitizeAsync(text, PromptInjectionSource.ModelAnswer(sourceName));
        return new(filteredText, sourceRestrictions.WithWebAddressesWrittenByModelIn(filteredText));
    }

    /// <summary>
    /// Prepares the text of a message of a chat for sending, together with what the chat demands.
    /// </summary>
    /// <remarks>
    /// A message of the AI counts as written by a model, a message of the user does not, see the overload
    /// for a text. The restrictions are those of the chat as it is now: a message of the user written before
    /// the chat read a mailbox demands what the chat demands since, which is the safe side.
    /// </remarks>
    /// <param name="sourceChat">The chat the message belongs to.</param>
    /// <param name="content">The content of the message.</param>
    /// <returns>The text to send, with its restrictions, or null when the content is no message of the chat with a text to send.</returns>
    public async Task<TextLaunchPayload?> CreateTextPayloadAsync(ChatThread sourceChat, IContent content)
    {
        var block = sourceChat.Blocks.FirstOrDefault(candidate => ReferenceEquals(candidate.Content, content));
        if (block is not { Role: ChatRole.AI or ChatRole.USER, Content: ContentText { Text: var text } } || string.IsNullOrWhiteSpace(text))
            return null;

        var sourceName = string.IsNullOrWhiteSpace(sourceChat.Name) ? TB("Unnamed chat") : sourceChat.Name;
        return await this.CreateTextPayloadAsync(text, writtenByModel: block.Role is ChatRole.AI, sourceChat.CaptureRestrictions(), sourceName);
    }

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
