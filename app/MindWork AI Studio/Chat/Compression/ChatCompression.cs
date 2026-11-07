using System.Globalization;
using System.Text.RegularExpressions;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Chat.Compression;

/// <summary>
/// Makes the new chat a compressed chat continues in.
/// </summary>
/// <remarks>
/// The new chat starts as a copy of the compressed one, see WorkspaceBehaviour.CopyChatAsync, so it
/// keeps everything which is not a message: the workspace, the profile, the chat template, the
/// tools, the data sources, the system prompt, and above all the restrictions, which say what the
/// conversation has seen and which providers and tools may still see it. Only the messages are
/// replaced, before the copy is stored for the first time.
/// </remarks>
public static class ChatCompression
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatCompression).Namespace, nameof(ChatCompression));

    private static readonly TimeSpan REGEX_TIMEOUT = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Replaces the messages of a copy of the compressed chat with the summary and the last answer of the AI.
    /// </summary>
    /// <remarks>
    /// The plan is made anew on the copy, which is the compressed chat as it was serialized, so the
    /// copy keeps blocks and attachments of its own and shares none with the original. The new chat
    /// then holds the example conversation of its chat template, the summary as a message of the
    /// user, and the last answer of the AI. The summary goes right before that answer, which keeps
    /// its time, so the order of the time stamps is the order of the conversation.<br/><br/>
    ///
    /// A message of the user which had no answer becomes the draft of the new chat, so the user finds
    /// it in the field for the next message, see ChatCompressionPlan.<br/><br/>
    ///
    /// What described the last request of the compressed chat is reset, as after a rollback: the data
    /// retrieved for it and the data sources an agent picked. The restrictions only tighten, by those
    /// the summary brings along, see AssistantLaunchService.CreateTextPayloadAsync.
    /// </remarks>
    /// <param name="continuation">The copy of the compressed chat, not stored yet.</param>
    /// <param name="exampleConversationLength">How many blocks the example conversation of the chat template holds, 0 without a template.</param>
    /// <param name="summaryMessage">The text of the message which holds the summary, see SummaryMessage.</param>
    /// <param name="summaryRestrictions">What the filtered summary demands, which the new chat demands as well.</param>
    /// <param name="providerId">The provider the new chat continues with: the one which wrote the summary.</param>
    /// <exception cref="InvalidOperationException">The copy holds nothing to compress, which only happens when it is not a copy of a chat which had something.</exception>
    public static void TurnIntoContinuation(ChatThread continuation, int exampleConversationLength, string summaryMessage, ChatRestrictions summaryRestrictions, string providerId)
    {
        var plan = ChatCompressionPlan.Of(continuation, exampleConversationLength, continuation.Draft.IsEmpty)
                   ?? throw new InvalidOperationException("The chat holds nothing to compress.");

        var summary = new ContentBlock
        {
            Time = plan.LastAnswer.Time.AddTicks(-1),
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = new ContentText
            {
                Text = summaryMessage,
            },
        };

        //
        // What the provider reported about the last answer was for the request of the compressed
        // chat, see ChatThread.ReportedHistoryFor:
        //
        if (plan.LastAnswer.Content is ContentText lastAnswer)
            lastAnswer.ReportedUsage = null;

        continuation.Blocks = [..plan.Preamble, summary, plan.LastAnswer];
        if (plan.PendingQuestion is { } pendingQuestion)
        {
            continuation.Draft.SetText(pendingQuestion.Text);
            continuation.Draft.AddAttachments(pendingQuestion.FileAttachments);
        }

        continuation.AugmentedData = string.Empty;
        continuation.AISelectedDataSources = [];
        continuation.RequireRestrictions(summaryRestrictions);
        continuation.SelectedProvider = providerId;
    }

    /// <summary>
    /// The text of the message which holds the summary in the new chat.
    /// </summary>
    /// <remarks>
    /// The user reads it, so it says where the conversation comes from and that its files did not
    /// come along. The AI reads it as well, which does no harm: it learns the same.
    /// </remarks>
    /// <param name="sourceName">The name of the compressed chat.</param>
    /// <param name="summary">The summary, filtered for prompt injections.</param>
    /// <returns>The text of the message.</returns>
    public static string SummaryMessage(string sourceName, string summary)
    {
        var heading = string.Format(TB("Summary of the chat '{0}'"), sourceName);
        var note = TB("This chat continues the conversation of that chat, which stays as it was. Files attached there are not part of this chat: attach them again when the AI needs their full content.");
        return $"**{heading}**\n\n{note}\n\n---\n\n{summary.Trim()}";
    }

    /// <summary>
    /// The name of a chat as the summary and the suggested name of its continuation call it.
    /// </summary>
    /// <param name="chatName">The name of the chat, which may be empty.</param>
    /// <returns>The name, or a placeholder for a chat without one.</returns>
    public static string NameOf(string chatName) => string.IsNullOrWhiteSpace(chatName) ? TB("Unnamed chat") : chatName;

    /// <summary>
    /// The name suggested for the new chat.
    /// </summary>
    /// <param name="sourceName">The name of the compressed chat, which may be empty.</param>
    /// <returns>The suggested name, e.g., "Plan (continued)", or "Plan (continued 2)" for a chat which continues one already.</returns>
    public static string NameOfContinuation(string sourceName) => ContinuedName(NameOf(sourceName), TB("{0} (continued)"), TB("{0} (continued {1})"));

    /// <summary>
    /// Names the continuation of a chat, counting up when the chat continues one already.
    /// </summary>
    /// <remarks>
    /// Kept apart from the translations so that a test can ask it with formats of its own. A chat
    /// compressed several times thus becomes "Plan (continued 3)" rather than "Plan (continued)
    /// (continued) (continued)".
    /// </remarks>
    /// <param name="name">The name of the compressed chat.</param>
    /// <param name="onceFormat">The format of a first continuation, with {0} for the name.</param>
    /// <param name="countedFormat">The format of a further continuation, with {0} for the name and {1} for the number.</param>
    /// <returns>The name of the continuation.</returns>
    internal static string ContinuedName(string name, string onceFormat, string countedFormat)
    {
        var counted = MatchFormat(name, countedFormat);
        if (counted is not null && int.TryParse(counted.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number < int.MaxValue)
            return string.Format(CultureInfo.InvariantCulture, countedFormat, counted.Groups["name"].Value, number + 1);

        var once = MatchFormat(name, onceFormat);
        if (once is not null)
            return string.Format(CultureInfo.InvariantCulture, countedFormat, once.Groups["name"].Value, 2);

        return string.Format(CultureInfo.InvariantCulture, onceFormat, name);
    }

    private static Match? MatchFormat(string name, string format)
    {
        var pattern = "^" + Regex.Escape(format).Replace(@"\{0}", "(?<name>.+)").Replace(@"\{1}", "(?<number>[0-9]+)") + "$";
        var match = Regex.Match(name, pattern, RegexOptions.CultureInvariant, REGEX_TIMEOUT);
        return match.Success && match.Groups["name"].Success ? match : null;
    }
}