using AIStudio.Chat;
using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Hands the guidelines of an organization for public appearances, see DataBeyondFactsLab.OrganizationRules, to the
/// chats of the steps of the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The guidelines inform; they are no instructions for the model. They therefore stay out of the system prompt, where
/// they could outweigh the rules of the lab, and go along with the first message of a chat instead, marked as coming
/// from the organization; the core prompt tells the model how to treat them. Only the request carries them, as it
/// carries the images of the material: the stored chat keeps the first message as it was, and every request carries
/// the guidelines as they are now. A message of their own in front of the first one would not do: two messages of the
/// user in a row break the chat templates of some self-hosted models, and the request has to keep as many messages as
/// the chat, see ContentText.CreateFromProviderAsync.
///
/// The conversation about whether the lab fits a task prepares no appearance, and the research must learn nothing but
/// the public details of the event, so neither of them gets the guidelines.
/// </remarks>
public static class BflOrganizationRules
{
    /// <summary>
    /// Gets the guidelines which the requests of a chat carry.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="settings">The settings of the lab.</param>
    /// <returns>The guidelines for a chat of a step, and nothing for every other chat.</returns>
    public static string For(BflThreadLocation location, DataBeyondFactsLab settings) => location is BflStepThreadLocation ? settings.OrganizationRules : string.Empty;

    /// <summary>
    /// Gets the text which the first message of a request carries in front, e.g., to count it beforehand.
    /// </summary>
    /// <remarks>
    /// German and tagged, like the sections of the prompts.
    /// </remarks>
    /// <param name="rules">The guidelines of the organization; empty without any.</param>
    /// <returns>The guidelines in their tags, followed by an empty line; nothing without guidelines.</returns>
    public static string Frame(string rules) => string.IsNullOrWhiteSpace(rules)
        ? string.Empty
        : $"<hinweise_der_organisation>\n{rules}\n</hinweise_der_organisation>\n\n";

    /// <summary>
    /// Gives a request the guidelines in front of the first message, without storing them in the chat.
    /// </summary>
    /// <remarks>
    /// The request gets a copy of that message only; every other message, the answer which streams in above all,
    /// stays the same object, and the chat keeps as many messages, see BflConversationService.WithImages.
    /// </remarks>
    /// <param name="thread">The chat.</param>
    /// <param name="rules">The guidelines of the organization; empty without any.</param>
    /// <returns>The chat for the request: the chat itself without guidelines or without a message yet.</returns>
    public static ChatThread WithRules(ChatThread thread, string rules)
    {
        if (string.IsNullOrWhiteSpace(rules))
            return thread;

        var index = thread.Blocks.FindIndex(block => block is { Role: ChatRole.USER, Content: ContentText });
        if (index < 0)
            return thread;

        var firstMessage = thread.Blocks[index].DeepClone();
        var content = (ContentText)firstMessage.Content!;
        content.Text = Frame(rules) + content.Text;

        List<ContentBlock> blocks = [.. thread.Blocks];
        blocks[index] = firstMessage;
        return thread with { Blocks = blocks };
    }
}