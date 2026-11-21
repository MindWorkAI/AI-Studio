using System.Globalization;

using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Web;

namespace AIStudio.Chat.Compression;

/// <summary>
/// Makes the chat a compressed chat continues in.
/// </summary>
/// <remarks>
/// The new chat starts as a copy of the compressed one, see WorkspaceBehaviour.CopyChatAsync, so it
/// keeps everything which is not a message: the workspace, the profile, the chat template, the
/// tools, the data sources, the system prompt, and above all the restrictions, which say what the
/// conversation has seen and which providers and tools may still see it. Only the messages are
/// replaced, before the copy is stored for the first time. An embedded chat of an assistant has no
/// workspace to keep the original in, so its copy replaces it, see ChatCompressionTarget.SAME_CHAT.
/// </remarks>
public static class ChatCompression
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatCompression).Namespace, nameof(ChatCompression));

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(ChatCompression).Namespace, nameof(ChatCompression));

    /// <summary>
    /// Stands for the name of the chat in the formats of a continued name.
    /// </summary>
    private const string NAME_PLACEHOLDER = "{0}";

    /// <summary>
    /// Stands for the number of the continuation in the formats of a continued name.
    /// </summary>
    private const string NUMBER_PLACEHOLDER = "{1}";

    /// <summary>
    /// Replaces the messages of a copy of the compressed chat with the summary and the last exchange.
    /// </summary>
    /// <remarks>
    /// The plan is made anew on the copy, which is the compressed chat as it was serialized, so the
    /// copy keeps blocks and attachments of its own and shares none with the original. The new chat
    /// then holds the example conversation of its chat template, the last message of the user with
    /// the summary ahead of its text, and the last answer of the AI. The last message keeps its time
    /// and its files, which the AI thus receives in full again, and the roles keep alternating, see
    /// ChatCompressionPlan. Answering that message anew in the new chat answers it with the summary
    /// at hand.<br/><br/>
    ///
    /// When the last message of the user was hidden and therefore summarized, the summary becomes a
    /// message of its own, right before the last answer.<br/><br/>
    ///
    /// A message of the user which had no answer becomes the draft of the new chat, so the user finds
    /// it in the field for the next message, see ChatCompressionPlan. Within the same chat, the user
    /// still has the message being written in that field, so such a message is summarized instead.<br/><br/>
    ///
    /// What described the last request of the compressed chat is reset, as after a rollback: the data
    /// retrieved for it and the data sources an AI operation picked. The restrictions only tighten, by those
    /// the summary brings along, see AssistantLaunchService.CreateTextPayloadAsync.<br/><br/>
    ///
    /// The last answer keeps what AI Studio noted about its tool calls and sources. The web addresses
    /// the tools returned for the summarized answers move to it, as far as the summary names them, see
    /// WebAddressesFromToolsNamedIn.
    /// </remarks>
    /// <param name="continuation">The copy of the compressed chat, not stored yet.</param>
    /// <param name="exampleConversationLength">How many blocks the example conversation of the chat template holds, 0 without a template.</param>
    /// <param name="target">Where the conversation goes on.</param>
    /// <param name="summaryMessage">The text of the message which holds the summary, see SummaryMessage.</param>
    /// <param name="summaryRestrictions">What the filtered summary demands, which the new chat demands as well.</param>
    /// <param name="providerId">The provider the new chat continues with: the one which wrote the summary.</param>
    /// <exception cref="InvalidOperationException">The copy holds nothing to compress, which only happens when it is not a copy of a chat which had something.</exception>
    public static void TurnIntoContinuation(ChatThread continuation, int exampleConversationLength, ChatCompressionTarget target, string summaryMessage, ChatRestrictions summaryRestrictions, string providerId)
    {
        var draftIsEmpty = target is ChatCompressionTarget.NEW_CHAT && continuation.Draft.IsEmpty;
        var plan = ChatCompressionPlan.Of(continuation, exampleConversationLength, draftIsEmpty)
                   ?? throw new InvalidOperationException("The chat holds nothing to compress.");

        ContentBlock summary;
        if (plan.LastQuestion is { Content: ContentText lastQuestion } lastQuestionBlock)
        {
            lastQuestion.Text = WithLastMessage(target, summaryMessage, lastQuestion.Text);
            summary = lastQuestionBlock;
        }
        else
        {
            summary = new ContentBlock
            {
                Time = plan.LastAnswer.Time.AddTicks(-1),
                ContentType = ContentType.TEXT,
                Role = ChatRole.USER,
                Content = new ContentText
                {
                    Text = summaryMessage,
                },
            };
        }

        if (plan.LastAnswer.Content is ContentText lastAnswer)
        {
            // What the provider reported about the last answer was for the request of the compressed chat, see ChatThread.ReportedHistoryFor:
            lastAnswer.ReportedUsage = null;
            lastAnswer.WebAddressesFromTools.UnionWith(WebAddressesFromToolsNamedIn(plan.ToSummarize, summaryMessage));
        }

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
    /// The web addresses the tools returned for the summarized answers which the summary still names, as request keys.
    /// </summary>
    /// <remarks>
    /// Each answer keeps the addresses its tools returned and a later message still shows the model,
    /// so that the model may read such a page again while its free address choice is off, see
    /// ContentText.WebAddressesFromTools. The summarized answers are gone from the new chat, and their
    /// addresses with them. Those the summary names are still shown to the model, so the rule which
    /// kept them holds for them in the new chat as well. Any other address in the summary stays one a
    /// model wrote, see ChatThread.WebAddressesWrittenByModels.
    /// </remarks>
    /// <param name="summarized">The blocks the summary replaces.</param>
    /// <param name="summaryMessage">The text of the message which holds the summary, see SummaryMessage.</param>
    /// <returns>The request keys, see WebAddresses.CreateRequestKey.</returns>
    internal static IEnumerable<string> WebAddressesFromToolsNamedIn(IEnumerable<ContentBlock> summarized, string summaryMessage)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var address in WebAddresses.Find(summaryMessage))
        {
            if (WebAddresses.TryCreateRequestKey(address, out var requestKey))
                named.Add(requestKey);
        }

        return summarized
            .Where(block => block.Role is ChatRole.AI)
            .Select(block => block.Content)
            .OfType<ContentText>()
            .SelectMany(answer => answer.WebAddressesFromTools)
            .Where(named.Contains)
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    /// The text of the message which holds the summary in the chat the conversation continues in.
    /// </summary>
    /// <remarks>
    /// The user reads it, so it says where the conversation comes from and that the summary only
    /// describes the files attached there. The AI reads it as well, which does no harm: it learns
    /// the same.
    /// </remarks>
    /// <param name="target">Where the conversation goes on.</param>
    /// <param name="sourceName">The name of the compressed chat; only a new chat names it.</param>
    /// <param name="summary">The summary, filtered for prompt injections.</param>
    /// <returns>The text of the message.</returns>
    public static string SummaryMessage(ChatCompressionTarget target, string sourceName, string summary)
    {
        if (target is ChatCompressionTarget.SAME_CHAT)
        {
            var heading = TB("Summary of the earlier conversation");
            var note = TA("The summary takes the place of the earlier messages of this conversation and describes the files attached there. The files of your last message stay attached to it; attach any other file again when the AI needs its full content.");
            return $"**{heading}**\n\n{note}\n\n---\n\n{summary.Trim()}";
        }

        var chatHeading = string.Format(TB("Summary of the chat '{0}'"), sourceName);
        var chatNote = TA("This chat continues the conversation of that chat, which stays as it was. The summary describes the files attached there. The files of your last message stay attached to it; attach any other file again when the AI needs its full content.");
        return $"**{chatHeading}**\n\n{chatNote}\n\n---\n\n{summary.Trim()}";
    }

    /// <summary>
    /// The text of the last message of the user after compressing: the summary, followed by the message as it was.
    /// </summary>
    /// <param name="target">Where the conversation goes on.</param>
    /// <param name="summaryMessage">The text of the message which holds the summary, see SummaryMessage.</param>
    /// <param name="lastMessage">The text of the last message of the user.</param>
    /// <returns>The text of the message.</returns>
    internal static string WithLastMessage(ChatCompressionTarget target, string summaryMessage, string lastMessage)
    {
        var heading = target is ChatCompressionTarget.SAME_CHAT ? TA("Your last message") : TA("Your last message in that chat");
        return $"{summaryMessage}\n\n---\n\n**{heading}**\n\n{lastMessage}";
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
        if (TryReadName(name, countedFormat, out var countedName, out var number) && number < int.MaxValue)
            return string.Format(CultureInfo.InvariantCulture, countedFormat, countedName, number + 1);

        if (TryReadName(name, onceFormat, out var onceName, out _))
            return string.Format(CultureInfo.InvariantCulture, countedFormat, onceName, 2);

        return string.Format(CultureInfo.InvariantCulture, onceFormat, name);
    }

    /// <summary>
    /// Reads the name of the compressed chat, and the number where the format has one, back out of a name written with the given format.
    /// </summary>
    /// <remarks>
    /// The format comes from the translation, so it can only be known while the app runs: a regular
    /// expression could not be generated while compiling, and none is needed. The text around the
    /// placeholders is compared as it stands, with {0} for a name of at least one character and {1}
    /// for digits, in whichever order the translation puts them. When the name holds the text between
    /// the placeholders more than once, the name of the chat takes the most, as in "Plan (continued 2)
    /// (continued 3)", whose chat is "Plan (continued 2)".
    /// </remarks>
    /// <param name="name">The name to read.</param>
    /// <param name="format">The format, with {0} for the name of the chat and, optionally, {1} for the number.</param>
    /// <param name="chatName">The name of the chat, when the name was written with the format; otherwise empty.</param>
    /// <param name="number">The number, when the format has one and the name was written with it; otherwise 0.</param>
    /// <returns>True when the name was written with the format.</returns>
    private static bool TryReadName(string name, string format, out string chatName, out int number)
    {
        chatName = string.Empty;
        number = 0;

        var namePosition = format.IndexOf(NAME_PLACEHOLDER, StringComparison.Ordinal);
        if (namePosition < 0)
            return false;

        // Both placeholders are equally long, so the length of the one steps over the other as well:
        var numberPosition = format.IndexOf(NUMBER_PLACEHOLDER, StringComparison.Ordinal);
        var firstPosition = numberPosition < 0 ? namePosition : Math.Min(namePosition, numberPosition);
        var lastPosition = numberPosition < 0 ? namePosition : Math.Max(namePosition, numberPosition);
        var prefix = format[..firstPosition];
        var suffix = format[(lastPosition + NAME_PLACEHOLDER.Length)..];
        if (name.Length < prefix.Length + suffix.Length || !name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal))
            return false;

        var inner = name[prefix.Length..^suffix.Length];
        if (numberPosition < 0)
        {
            chatName = inner;
            return chatName.Length > 0;
        }

        var separator = format[(firstPosition + NAME_PLACEHOLDER.Length)..lastPosition];
        string digits;
        if (numberPosition < namePosition)
        {
            var digitCount = 0;
            while (digitCount < inner.Length && char.IsAsciiDigit(inner[digitCount]))
                digitCount++;

            var rest = inner[digitCount..];
            if (digitCount is 0 || !rest.StartsWith(separator, StringComparison.Ordinal))
                return false;

            digits = inner[..digitCount];
            chatName = rest[separator.Length..];
        }
        else
        {
            var digitCount = 0;
            while (digitCount < inner.Length && char.IsAsciiDigit(inner[^(digitCount + 1)]))
                digitCount++;

            var rest = inner[..^digitCount];
            if (digitCount is 0 || !rest.EndsWith(separator, StringComparison.Ordinal))
                return false;

            digits = inner[^digitCount..];
            chatName = rest[..^separator.Length];
        }

        return chatName.Length > 0 && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}