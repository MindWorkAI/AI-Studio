using AIStudio.Chat;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The part of the SWOT analysis assistant which starts a chat from its result.
/// </summary>
public partial class AssistantSWOTAnalysis
{
    /// <summary>
    /// The chat gets the visible request and the result only, not the hidden request behind it.
    /// </summary>
    /// <remarks>
    /// The hidden request holds what the models of the four categories wrote, web addresses from the
    /// research among it. A chat counts every web address in a message of the user as given to the
    /// model, see ChatThread.IsWebAddressGivenToTheModel. Carried along, an address a model chose
    /// would pass for one the user gave, and a switched-off free address choice would no longer
    /// hold. The restrictions of the thread go along, those taken over from the research included.
    /// </remarks>
    protected override ChatThread ConvertToChatThread
    {
        get
        {
            var result = this.ResultingContentBlock;
            var visiblePrompt = result is null ? null : this.CreateSendToChatPrompt([result]);
            return CreateChatThreadForResult(this.ChatThread ?? new ChatThread(), visiblePrompt, result);
        }
    }

    /// <summary>
    /// Creates the thread of a chat which starts from the result of an analysis.
    /// </summary>
    /// <param name="assistantThread">The thread of the assistant, whose restrictions the chat keeps.</param>
    /// <param name="visiblePrompt">The visible request of the user; null when there is no result.</param>
    /// <param name="result">The result of the analysis; null when there is none.</param>
    /// <returns>The thread of the chat, with the visible request and the visible result as its only blocks.</returns>
    internal static ChatThread CreateChatThreadForResult(ChatThread assistantThread, ContentBlock? visiblePrompt, ContentBlock? result) => assistantThread with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
        Blocks = visiblePrompt is null || result is null
            ? []

            // Visible, also when the answer came hidden: the result is what the user asked for.
            : [visiblePrompt, result.DeepClone(changeHideState: true, hideFromUser: false)],
    };
}