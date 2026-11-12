using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Security;
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
/// <param name="promptInjectionGuard">Reports what the filter took out of the attached documents, once for the whole compression.</param>
/// <param name="logger">The logger.</param>
public sealed class ChatCompressionService(ConversationTokenCounter tokenCounter, PromptInjectionGuardService promptInjectionGuard, ILogger<ChatCompressionService> logger)
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
    /// Context windows of 250,000 tokens and more are common by now. A summary of 8,192 tokens takes
    /// a small share of them, yet keeps room for the details of a long conversation. A summary meant
    /// to save tokens has no business being longer anyway.
    /// </remarks>
    private const int MAX_TOKENS_FOR_THE_SUMMARY = 8_192;

    /// <summary>
    /// The room kept free for the words of a request around the messages, and for the rounding of estimates.
    /// </summary>
    private const int TOKENS_FOR_THE_REQUEST_FRAME = 512;

    /// <summary>
    /// The smallest context window a model needs for compressing a chat.
    /// </summary>
    /// <remarks>
    /// The system prompt, the room for the summary, and the summary so far take about 4,600 tokens
    /// before a single message fits. Close to that, every part holds only a few messages, and the
    /// summary is written anew for each of them. From 8,192 tokens on, a part holds a few
    /// thousand tokens of the conversation.
    /// </remarks>
    public const int MIN_CONTEXT_WINDOW = 8_192;

    /// <summary>
    /// Whether a model with the given context window can compress a chat.
    /// </summary>
    /// <remarks>
    /// An unknown window does not stop it: the whole conversation then goes into one request, and
    /// the provider tells when it is too long, see SummarizeAsync.
    /// </remarks>
    /// <param name="window">The context window of the model which writes the summary.</param>
    /// <returns>True when the window is unknown or at least MIN_CONTEXT_WINDOW.</returns>
    public static bool CanSummarizeWith(ContextWindow window) => !window.IsKnown || window.DefaultTokens >= MIN_CONTEXT_WINDOW;

    /// <summary>
    /// How many tokens the summary may take in a model with the given context window.
    /// </summary>
    /// <remarks>
    /// A quarter of the window, at least MIN_TOKENS_FOR_THE_SUMMARY and at most MAX_TOKENS_FOR_THE_SUMMARY.
    /// The new chat starts with a summary of this length at most.
    /// </remarks>
    /// <param name="windowTokens">The context window of the model which writes the summary.</param>
    /// <returns>The tokens kept free for the summary.</returns>
    public static int TokensForTheSummary(int windowTokens) => Math.Clamp(windowTokens / 4, MIN_TOKENS_FOR_THE_SUMMARY, MAX_TOKENS_FOR_THE_SUMMARY);

    /// <summary>
    /// Asks the AI for a summary of the messages the plan names.
    /// </summary>
    /// <remarks>
    /// The summary takes in the summarized messages as the AI received them in the chat, with the
    /// text of their documents and transcripts, see ContentText.PrepareTextContentForAI. A document
    /// stored at its first send comes from the provenance store, so the summary reads what the AI
    /// read, even when its file changed or is gone by now. The last exchange goes along as well,
    /// marked, so that the summary leads up to it, see ChatCompressionInput.Render.<br/><br/>
    ///
    /// Images do not go along. What matters of an image for the conversation shows in what the
    /// conversation said about it, the answers of the AI above all, and the summary keeps that.
    /// Describing every image anew would cost a request of its own per image, which adds up to
    /// hundreds in a chat full of screenshots.<br/><br/>
    ///
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

        var window = provider.GetModelProfile().Context;
        if (!CanSummarizeWith(window))
            return ChatCompressionResult.Failed(TB("The context window of the selected model is too small to summarize this chat. Please select a model which takes longer texts."));

        try
        {
            progress?.Report(new(ChatCompressionStep.READING_FILES, 0, 0));
            var texts = await this.ReadMessagesAsync(plan.ToSummarize, token);

            //
            // The last exchange goes along at its place in the conversation, marked, so that the
            // summary knows what follows it. Messages after it, such as an unanswered question, keep
            // coming after it, so the AI does not take the answer for a reply to them, see
            // ChatCompressionPlan.ToRead:
            //
            var kept = new Dictionary<ContentBlock, KeptMessage>(ReferenceEqualityComparer.Instance) { [plan.LastAnswer] = KeptMessage.LAST_ANSWER };
            if (plan.LastQuestion is not null)
                kept[plan.LastQuestion] = KeptMessage.LAST_QUESTION;

            var messages = new List<CountedMessage>(plan.ToRead.Count);
            var number = 0;
            foreach (var block in plan.ToRead)
            {
                var keptMessage = kept.GetValueOrDefault(block, KeptMessage.NONE);
                var text = keptMessage is KeptMessage.NONE ? texts[block] : (block.Content as ContentText)?.Text ?? string.Empty;
                messages.Add(await this.CountAsync(provider, ChatCompressionInput.Render(block, ++number, text, keptMessage), token));
            }

            var tokensForTheSummary = window.IsKnown ? TokensForTheSummary(window.DefaultTokens) : 0;
            var tokensForMessagesAndSummary = window.IsKnown ? await this.TokensForMessagesAndSummaryAsync(provider, window.DefaultTokens, tokensForTheSummary, focus, token) : int.MaxValue;

            // There has to be room for a part next to a summary so far which takes all the room kept for it, which a long focus may take away:
            if (window.IsKnown && tokensForMessagesAndSummary - tokensForTheSummary < MIN_TOKENS_PER_PART)
                return ChatCompressionResult.Failed(TB("The context window of the selected model is too small to summarize this chat. Please select a model which takes longer texts."));

            // A word takes more than one token in most languages, so half of the tokens is a safe number of words:
            var maxWords = tokensForTheSummary / 2;

            // Only for showing the progress: every part after the first one shares its room with the summary so far:
            var expectedParts = window.IsKnown ? ChatCompressionInput.SplitIntoParts(messages, tokensForMessagesAndSummary - tokensForTheSummary).Count : 1;

            var summary = string.Empty;
            var summaryTokens = 0;
            var (startMessage, startOffset) = (0, 0);
            var part = 0;
            while (startMessage < messages.Count)
            {
                token.ThrowIfCancellationRequested();

                var tokensPerPart = window.IsKnown ? tokensForMessagesAndSummary - summaryTokens : int.MaxValue;
                if (tokensPerPart < MIN_TOKENS_PER_PART)
                    return ChatCompressionResult.Failed(TB("The summary grew too long for the context window of the selected model. Please select a model which takes longer texts."));

                var (conversation, nextMessage, nextOffset) = ChatCompressionInput.TakePart(messages, startMessage, startOffset, tokensPerPart);
                var isLastPart = nextMessage >= messages.Count;
                part++;
                progress?.Report(new(ChatCompressionStep.SUMMARIZING, part, isLastPart ? part : Math.Max(part + 1, expectedParts)));

                var request = ChatCompressionPrompts.BuildRequest(conversation, part, isLastPart, summary, focus, maxWords);
                summary = await RequestSummaryAsync(sourceChat, provider, request, token);
                token.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(summary))
                    return ChatCompressionResult.Failed(TB("The AI did not write a summary. Please check the messages about the provider, and try again."));

                if (!isLastPart && window.IsKnown)
                    summaryTokens = (await this.CountAsync(provider, summary, token)).Tokens;

                (startMessage, startOffset) = (nextMessage, nextOffset);
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
    /// Reads the messages to summarize as the AI received them in the chat, with the text of their documents and transcripts.
    /// </summary>
    /// <remarks>
    /// The images stay out, see SummarizeAsync; the rendered message names them. Reading works on a
    /// copy of each message, so the chat stays untouched. What the filter takes out of the
    /// documents is reported once for all of them, as for a request of the chat, see
    /// ListContentBlockExtensions.BuildMessagesAsync.
    /// </remarks>
    /// <param name="blocks">The messages to summarize.</param>
    /// <param name="token">Ends the reading between two messages.</param>
    /// <returns>The texts by their blocks.</returns>
    private async Task<IReadOnlyDictionary<ContentBlock, string>> ReadMessagesAsync(IReadOnlyList<ContentBlock> blocks, CancellationToken token)
    {
        var texts = new Dictionary<ContentBlock, string>(ReferenceEqualityComparer.Instance);
        await using var reportingScope = blocks.Any(block => block.Content is ContentText text && text.FileAttachments.MayContainDocuments())
            ? promptInjectionGuard.BeginAction()
            : null;

        foreach (var block in blocks)
        {
            token.ThrowIfCancellationRequested();
            if (block.Content is not ContentText text)
            {
                texts[block] = string.Empty;
                continue;
            }

            var withoutImages = new ContentText
            {
                Text = text.Text,
                FileAttachments = [..text.FileAttachments.Where(attachment => !attachment.IsImage)],
            };

            texts[block] = await withoutImages.PrepareTextContentForAI(reportingScope);
        }

        return texts;
    }

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