using AIStudio.Chat;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The part of the SWOT analysis assistant which starts a chat from its result.
/// </summary>
public partial class AssistantSWOTAnalysis
{
    // The prompts of this assistant demand JSON, so the chat gets a prompt of its own, which keeps the method, the goal, and the language:
    protected override ChatSystemPromptChoice ChatSystemPrompt => ChatSystemPromptChoice.Custom(this.CreateChatSystemPrompt);

    /// <summary>
    /// Creates the system prompt of a chat which continues an analysis, for follow-up questions about it.
    /// </summary>
    /// <remarks>
    /// It holds what the user set, never what a model wrote, for the same reason the chat leaves out the
    /// hidden request: a web address in the system prompt counts as given by the user, see
    /// ChatThread.RuntimeSystemPromptWebAddresses. The research therefore stays out of it as well.
    /// </remarks>
    private string CreateChatSystemPrompt() =>
        $"""
        You are an expert in strategic analysis and know the SWOT method well: strengths and weaknesses are internal,
        opportunities and threats are external; strengths and opportunities are positive, weaknesses and threats are
        negative. The user had a SWOT analysis created: the conversation so far holds its result, with the findings of
        the four categories, the overall assessment, and the prioritized actions. Now you answer the user's follow-up
        questions about the analysis, e.g., why a finding belongs to its category, how two findings relate, or what an
        action involves, and you work out further steps when the user asks for them.

        The analysis rests on the content of the user and on research which this conversation does not hold. When an
        answer needs more than the result shows, say so, and mark what you add from general knowledge.

        {ChatPromptSetting("The user stated this goal for the analysis:", this.analysisGoal)}
        {ChatPromptSetting("The user asked the analysis to consider these aspects:", this.importantAspects)}

        When you write parts of the analysis anew, follow this rule for their language: {this.OutputLanguageInstruction}
        Otherwise, answer in the language the user writes in.
        """;

    /// <summary>
    /// Puts a setting of the user into the system prompt of the chat, or nothing when the user left it empty.
    /// </summary>
    /// <param name="introduction">The sentence which introduces the setting.</param>
    /// <param name="setting">The setting as the user wrote it.</param>
    /// <returns>The introduction and the setting, or an empty string.</returns>
    private static string ChatPromptSetting(string introduction, string setting) => string.IsNullOrWhiteSpace(setting) ? string.Empty : $"""
        {introduction}
        {setting.Trim()}
        """;

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
        Blocks = visiblePrompt is null || result is null
            ? []

            // Visible, also when the answer came hidden: the result is what the user asked for.
            : [visiblePrompt, result.DeepClone(changeHideState: true, hideFromUser: false)],
    };
}