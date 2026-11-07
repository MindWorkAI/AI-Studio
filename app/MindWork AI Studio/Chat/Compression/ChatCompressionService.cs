using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Chat.Compression;

/// <summary>
/// Asks the AI to summarize a chat, so that the conversation can continue in a new chat which costs far fewer tokens.
/// </summary>
/// <remarks>
/// The summary is written by the provider the chat would send its next message to. It reads
/// everything the chat holds, so it has to be a provider the chat allows, and its requests carry
/// the restrictions of the chat, see ChatThread.RequireRestrictionsOf.<br/><br/>
///
/// What comes back is what the AI wrote. Before it reaches a model again, as a message of the new
/// chat, it has to pass the prompt injection filter, see AssistantLaunchService.CreateTextPayloadAsync.
/// </remarks>
/// <param name="tokenCounter">Counts the tokens of the messages, with the tokenizer of the provider.</param>
/// <param name="logger">The logger.</param>
public sealed class ChatCompressionService(ConversationTokenCounter tokenCounter, ILogger<ChatCompressionService> logger)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatCompressionService).Namespace, nameof(ChatCompressionService));

    /// <summary>
    /// The fewest tokens of messages a part has to hold for a summary to make sense.
    /// </summary>
    private const int MIN_TOKENS_PER_PART = 1_024;

    /// <summary>
    /// The least room kept free for the summary the AI writes.
    /// </summary>
    private const int MIN_TOKENS_FOR_THE_SUMMARY = 1_024;

    /// <summary>
    /// The most room kept free for the summary the AI writes.
    /// </summary>
    /// <remarks>
    /// Some providers stop an answer after a few thousand tokens unless told otherwise; Anthropic
    /// does so after 4,096. A summary which asked for more would end in the middle without anybody
    /// noticing, and the new chat would lack its last sections. A summary meant to save tokens has
    /// no business being longer anyway.
    /// </remarks>
    private const int MAX_TOKENS_FOR_THE_SUMMARY = 4_096;

    /// <summary>
    /// The room kept free for the words of a request around the messages, and for the rounding of estimates.
    /// </summary>
    private const int TOKENS_FOR_THE_REQUEST_FRAME = 512;

    /// <summary>
    /// Asks the AI for a summary of the messages the plan names.
    /// </summary>
    /// <remarks>
    /// A conversation which does not fit into one request is summarized in parts, one after the
    /// other, see ChatCompressionInput.TakePart. Each part gets what the context window of the model
    /// leaves after the summary so far, which is counted anew after every answer: a model may write
    /// more than it was asked to, and some languages take several tokens per word. When the window
    /// is unknown, the whole conversation goes into one request, and the provider tells when it is
    /// too long.
    /// </remarks>
    /// <param name="sourceChat">The chat to summarize. It stays unchanged.</param>
    /// <param name="plan">Which messages to summarize, see ChatCompressionPlan.Of.</param>
    /// <param name="provider">The provider which writes the summary.</param>
    /// <param name="focus">What the user wants the summary to pay particular attention to, or an empty string.</param>
    /// <param name="progress">Learns which part is being summarized, or null.</param>
    /// <param name="token">Ends the summarizing.</param>
    /// <returns>The summary, or why there is none.</returns>
    public async Task<ChatCompressionResult> SummarizeAsync(ChatThread sourceChat, ChatCompressionPlan plan, AIStudio.Settings.Provider provider, string focus, IProgress<ChatCompressionProgress>? progress, CancellationToken token)
    {
        if (provider == AIStudio.Settings.Provider.NONE)
            return ChatCompressionResult.Failed(TB("Please select a provider first."));

        if (!sourceChat.IsLLMProviderAllowed(provider))
            return ChatCompressionResult.Failed(TB("The selected provider is not allowed in this chat due to data security or confidence-level requirements. Please select a different provider."));

        try
        {
            //
            // The last answer goes along at its place in the conversation, marked, so that the summary
            // knows what follows it. Messages after it, such as an unanswered question, keep coming
            // after it, so the AI does not take the answer for a reply to them:
            //
            var messages = new List<CountedMessage>(plan.ToSummarize.Count + 1);
            var number = 0;
            foreach (var block in plan.ToSummarize.Append(plan.LastAnswer).OrderBy(block => block.Time))
                messages.Add(await this.CountAsync(provider, ChatCompressionInput.Render(block, ++number, isLastAnswer: ReferenceEquals(block, plan.LastAnswer)), token));

            var window = provider.GetModelProfile().Context;
            var tokensForTheSummary = window.IsKnown ? Math.Clamp(window.DefaultTokens / 4, MIN_TOKENS_FOR_THE_SUMMARY, MAX_TOKENS_FOR_THE_SUMMARY) : 0;
            var tokensForMessagesAndSummary = window.IsKnown ? await this.TokensForMessagesAndSummaryAsync(provider, window.DefaultTokens, tokensForTheSummary, focus, token) : int.MaxValue;

            // There has to be room for a part next to a summary so far which takes all the room kept for it:
            if (window.IsKnown && tokensForMessagesAndSummary - tokensForTheSummary < MIN_TOKENS_PER_PART)
                return ChatCompressionResult.Failed(TB("The context window of the selected model is too small to summarize this chat. Please select a model which takes longer texts."));

            // A word takes more than one token in most languages, so half of the tokens is a safe number of words:
            var maxWords = tokensForTheSummary / 2;

            // Only for showing the progress: every part after the first one shares its room with the summary so far:
            var expectedParts = window.IsKnown ? ChatCompressionInput.SplitIntoParts(messages, tokensForMessagesAndSummary - tokensForTheSummary).Count : 1;

            var summary = string.Empty;
            var summaryTokens = 0;
            var start = 0;
            var part = 0;
            while (start < messages.Count)
            {
                token.ThrowIfCancellationRequested();

                var tokensPerPart = window.IsKnown ? tokensForMessagesAndSummary - summaryTokens : int.MaxValue;
                if (tokensPerPart < MIN_TOKENS_PER_PART)
                    return ChatCompressionResult.Failed(TB("The summary grew too long for the context window of the selected model. Please select a model which takes longer texts."));

                var (conversation, next) = ChatCompressionInput.TakePart(messages, start, tokensPerPart);
                var isLastPart = next >= messages.Count;
                part++;
                progress?.Report(new(part, isLastPart ? part : Math.Max(part + 1, expectedParts)));

                var request = ChatCompressionPrompts.BuildRequest(conversation, part, isLastPart, summary, focus, maxWords);
                summary = await RequestSummaryAsync(sourceChat, provider, request, token);
                token.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(summary))
                    return ChatCompressionResult.Failed(TB("The AI did not write a summary. Please check the messages about the provider, and try again."));

                if (!isLastPart && window.IsKnown)
                    summaryTokens = (await this.CountAsync(provider, summary, token)).Tokens;

                start = next;
            }

            return ChatCompressionResult.Succeeded(summary);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return ChatCompressionResult.Failed(TB("Compressing the chat was canceled."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to summarize the chat '{ChatId}'.", sourceChat.ChatId);
            return ChatCompressionResult.Failed(string.Format(TB("The chat could not be summarized: {0}"), exception.Message));
        }
    }

    /// <summary>
    /// How many tokens one request may hold of messages and of the summary so far together.
    /// </summary>
    /// <remarks>
    /// The window of the model holds the system prompt, the words around the messages, what the user
    /// wants the summary to pay attention to, the summary so far, and the messages, and it has to
    /// leave room for the summary the AI writes.
    /// </remarks>
    /// <param name="provider">The provider which writes the summary.</param>
    /// <param name="windowTokens">The context window of its model.</param>
    /// <param name="tokensForTheSummary">The room kept for the summary the AI writes.</param>
    /// <param name="focus">What the user wants the summary to pay particular attention to, or an empty string.</param>
    /// <param name="token">Ends the counting.</param>
    /// <returns>The tokens left for messages and the summary so far, which may be less than nothing for a very small window.</returns>
    private async Task<int> TokensForMessagesAndSummaryAsync(AIStudio.Settings.Provider provider, int windowTokens, int tokensForTheSummary, string focus, CancellationToken token)
    {
        var systemPrompt = (await this.CountAsync(provider, ChatCompressionPrompts.SYSTEM_PROMPT, token)).Tokens;
        var focusTokens = string.IsNullOrWhiteSpace(focus) ? 0 : (await this.CountAsync(provider, focus, token)).Tokens;
        return windowTokens - systemPrompt - focusTokens - TOKENS_FOR_THE_REQUEST_FRAME - tokensForTheSummary;
    }

    /// <summary>
    /// Counts the tokens of a text with the tokenizer of the provider, or estimates them when it does not answer.
    /// </summary>
    /// <remarks>
    /// The estimate rather errs on the large side, three characters per token, so that a part does
    /// not grow beyond the window because a tokenizer was missing.
    /// </remarks>
    private async Task<CountedMessage> CountAsync(AIStudio.Settings.Provider provider, string text, CancellationToken token)
    {
        try
        {
            return new(text, await tokenCounter.CountTextAsync(provider, text, token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to count the tokens of a message to summarize. Estimating them instead.");
            return new(text, EstimateTokens(text));
        }
    }

    internal static int EstimateTokens(string text) => (text.Length + 2) / 3;

    /// <summary>
    /// Sends one request for a summary.
    /// </summary>
    /// <remarks>
    /// The request is a thread of its own, which nobody sees and nothing stores. It runs without
    /// tools and data sources, and it demands of the provider what the chat demands.
    /// </remarks>
    private static async Task<string> RequestSummaryAsync(ChatThread sourceChat, AIStudio.Settings.Provider provider, string request, CancellationToken token)
    {
        var time = DateTimeOffset.UtcNow;
        var userPrompt = new ContentText
        {
            Text = request,
        };

        var answer = new ContentText
        {
            InitialRemoteWait = true,
        };

        var thread = new ChatThread
        {
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = sourceChat.Name,
            SystemPrompt = ChatCompressionPrompts.SYSTEM_PROMPT,
            SelectedProvider = provider.Id,

            // Compressing belongs to the chat, so its requests name the chat:
            RuntimeComponent = AIStudio.Tools.Components.CHAT,
            Blocks =
            [
                new()
                {
                    Time = time,
                    ContentType = ContentType.TEXT,
                    Role = ChatRole.USER,
                    Content = userPrompt,
                    HideFromUser = true,
                },
                new()
                {
                    Time = time,
                    ContentType = ContentType.TEXT,
                    Role = ChatRole.AI,
                    Content = answer,
                    HideFromUser = true,
                },
            ],
        };

        // The request carries everything the chat holds, so it demands what the chat demands:
        thread.RequireRestrictionsOf(sourceChat);

        await answer.CreateFromProviderAsync(provider.CreateProvider(), provider.Model, userPrompt, thread, token);
        return answer.Text.RemoveThinkTags().Trim();
    }
}