using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.AIOperations.ConversationSummary;

/// <summary>
/// Summarizes a conversation, so that it can continue with far fewer tokens, e.g., when a chat is compressed.
/// </summary>
/// <remarks>
/// <para>
/// The summary reads everything the chat holds, so its provider has to be one the chat allows, and its
/// requests carry the restrictions of the chat, see ChatThread.RequireRestrictionsOf.
/// </para>
/// <para>
/// What comes back is what the AI wrote. Before it reaches a model again, as a message of the new chat, it has
/// to pass the prompt injection filter, see AssistantLaunchService.CreateTextPayloadAsync.
/// </para>
/// </remarks>
/// <param name="tokenCounter">Counts the tokens of the messages, with the tokenizer of the provider.</param>
/// <param name="promptInjectionGuard">Reports what the filter took out of the attached documents, once for the whole summary.</param>
/// <param name="logger">The logger.</param>
/// <param name="settingsManager">The settings, for the checks of the base right before each request.</param>
public sealed class ConversationSummaryOperation(ConversationTokenCounter tokenCounter, PromptInjectionGuardService promptInjectionGuard, ILogger<ConversationSummaryOperation> logger, SettingsManager settingsManager) : AIOperationBase<ConversationSummaryRequest, ConversationSummaryResult>(logger, settingsManager)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConversationSummaryOperation).Namespace, nameof(ConversationSummaryOperation));

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(ConversationSummaryOperation).Namespace, nameof(ConversationSummaryOperation));

    /// <summary>
    /// The fewest tokens of messages a part has to hold for a summary to make sense.
    /// </summary>
    private const int MIN_TOKENS_PER_PART = 1_024;

    /// <summary>
    /// The room kept free for the words of a request around the messages, and for the rounding of estimates.
    /// </summary>
    private const int TOKENS_FOR_THE_REQUEST_FRAME = 512;

    /// <summary>
    /// Reads the provider settings of the operation.
    /// </summary>
    /// <remarks>
    /// Compressing has no switch for its preselection: without a provider of its own, the model of the chat
    /// writes the summary, see DataChatCompression.
    /// </remarks>
    /// <param name="settingsManager">The settings to read.</param>
    /// <returns>The preferred provider and the minimum confidence.</returns>
    public static AIOperationProviderSettings GetProviderSettings(SettingsManager settingsManager)
    {
        var settings = settingsManager.ConfigurationData.ChatCompression;
        return new(settings.PreselectedProvider, settings.MinimumProviderConfidence);
    }

    #region Overrides of AIOperationBase

    /// <inheritdoc />
    public override string Id => "Conversation Summary";

    /// <inheritdoc />
    public override Tools.Components Component => Tools.Components.AI_TASK_CHAT_COMPRESSION;

    /// <summary>
    /// The summary reads everything the chat holds, so it requires what the chat requires.
    /// </summary>
    public override ConfidenceLevel GetRequiredConfidence(ConversationSummaryRequest request) => request.SourceChat.RequiredProviderConfidence;

    /// <summary>
    /// Asks the AI for a summary of the messages the plan names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The summary takes in the summarized messages as the AI received them in the chat, with the text of their
    /// documents and transcripts, see ContentText.PrepareTextContentForAI. A document stored at its first send
    /// comes from the provenance store, so the summary reads what the AI read, even when its file changed or is
    /// gone by now. The last exchange goes along as well, marked, so that the summary leads up to it, see
    /// ConversationSummaryInput.Render.
    /// </para>
    /// <para>
    /// Images do not go along. What matters of an image for the conversation shows in what the conversation said
    /// about it, the answers of the AI above all, and the summary keeps that. Describing every image anew would
    /// cost a request of its own per image, which adds up to hundreds in a chat full of screenshots.
    /// </para>
    /// <para>
    /// A conversation which does not fit into one request is summarized in parts, one after the other, see
    /// ConversationSummaryInput.TakePart. Each part gets what the context window of the model leaves after the
    /// summary so far, which is counted anew after every answer: a model may write more than it was asked to,
    /// and some languages take several tokens per word. When the window is unknown, the whole conversation goes
    /// into one request, and the provider tells when it is too long.
    /// </para>
    /// <para>
    /// A summary which grew beyond its tolerance is shortened by the AI in one request of its own, after a part
    /// as well as at the end, see ConversationSummaryBudget.IsTooLong. What comes back is taken as it is: a
    /// second round would cost another request and could not promise more, and the notice before sending tells
    /// the user when the new chat is still too long. A summary which the provider cut off, e.g., because it
    /// reached the most tokens the model writes at once, cannot be told apart from a complete one, since the
    /// providers do not report why an answer ended.
    /// </para>
    /// </remarks>
    public override async Task<ConversationSummaryResult> RunAsync(ConversationSummaryRequest request, AIOperationContext context, CancellationToken token = default)
    {
        var sourceChat = request.SourceChat;
        var plan = request.Plan;
        var provider = context.Provider;
        if (provider == AIStudio.Settings.Provider.NONE)
            return ConversationSummaryResult.Failed(TA("Please select a provider first."));

        if (!sourceChat.IsLLMProviderAllowed(provider))
            return ConversationSummaryResult.Failed(TA("The selected provider is not allowed in this chat due to data security or confidence-level requirements. Please select a different provider."));

        var window = provider.GetModelProfile().Context;
        if (!ConversationSummaryBudget.CanSummarizeWith(window))
            return ConversationSummaryResult.Failed(TA("The context window of the selected model is too small to summarize this chat. Please select a model which takes longer texts."));

        try
        {
            request.Progress?.Report(new(ConversationSummaryStep.READING_FILES, 0, 0));
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
                messages.Add(await this.CountAsync(provider, ConversationSummaryInput.Render(block, ++number, text, keptMessage), token));
            }

            var tokensForTheSummary = window.IsKnown ? ConversationSummaryBudget.TokensForTheSummary(window.DefaultTokens, request.MaxSummaryTokens) : 0;
            var tokensForMessagesAndSummary = window.IsKnown ? await this.TokensForMessagesAndSummaryAsync(provider, window.DefaultTokens, tokensForTheSummary, request.Focus, token) : int.MaxValue;

            // There has to be room for a part next to a summary so far which takes all the room kept for it, which a long focus may take away:
            if (window.IsKnown && tokensForMessagesAndSummary - tokensForTheSummary < MIN_TOKENS_PER_PART)
                return ConversationSummaryResult.Failed(TA("The context window of the selected model is too small to summarize this chat. Please select a model which takes longer texts."));

            // A word takes more than one token in most languages, so half of the tokens is a safe number of words:
            var maxWords = tokensForTheSummary / 2;

            // Only for showing the progress: every part after the first one shares its room with the summary so far:
            var expectedParts = window.IsKnown ? ConversationSummaryInput.SplitIntoParts(messages, tokensForMessagesAndSummary - tokensForTheSummary).Count : 1;

            var summary = string.Empty;
            var summaryTokens = 0;
            var position = ConversationSummaryPosition.START;
            var part = 0;
            while (position.Message < messages.Count)
            {
                token.ThrowIfCancellationRequested();

                var tokensPerPart = window.IsKnown ? tokensForMessagesAndSummary - summaryTokens : int.MaxValue;
                if (tokensPerPart < MIN_TOKENS_PER_PART)
                    return ConversationSummaryResult.Failed(TA("The summary grew too long for the context window of the selected model. Please select a model which takes longer texts."));

                var conversation = ConversationSummaryInput.TakePart(messages, position, tokensPerPart);
                var isLastPart = conversation.Next.Message >= messages.Count;
                part++;
                request.Progress?.Report(new(ConversationSummaryStep.SUMMARIZING, part, isLastPart ? part : Math.Max(part + 1, expectedParts)));

                var summaryRequest = ConversationSummaryPrompts.BuildRequest(conversation.Text, part, isLastPart, summary, request.Focus, maxWords);
                summary = await this.RequestSummaryAsync(request, context, summaryRequest, token);
                token.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(summary))
                    return ConversationSummaryResult.Failed(TA("The AI did not write a summary. Please check the messages about the provider, and try again."));

                if (window.IsKnown)
                {
                    summaryTokens = (await this.CountAsync(provider, summary, token)).Tokens;
                    if (ConversationSummaryBudget.IsTooLong(summaryTokens, tokensForTheSummary))
                    {
                        request.Progress?.Report(new(ConversationSummaryStep.CONDENSING, part, isLastPart ? part : Math.Max(part + 1, expectedParts)));
                        var shortened = await this.RequestSummaryAsync(request, context, ConversationSummaryPrompts.BuildShorteningRequest(summary, maxWords), token);
                        token.ThrowIfCancellationRequested();

                        // An empty answer shortens nothing, so the longer summary is still better than none:
                        if (!string.IsNullOrWhiteSpace(shortened))
                        {
                            summary = shortened;
                            summaryTokens = (await this.CountAsync(provider, summary, token)).Tokens;
                        }
                    }
                }

                position = conversation.Next;
            }

            return ConversationSummaryResult.Succeeded(summary);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return ConversationSummaryResult.Failed(TB("Compressing the chat was canceled."));
        }
        catch (Exception exception)
        {
            this.Logger.LogError(exception, "Failed to summarize the chat '{ChatId}'.", sourceChat.ChatId);
            return ConversationSummaryResult.Failed(string.Format(TB("The chat could not be summarized: {0}"), exception.Message));
        }
    }

    #endregion

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
        var systemPrompt = (await this.CountAsync(provider, ConversationSummaryPrompts.SYSTEM_PROMPT, token)).Tokens;
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
            this.Logger.LogWarning(exception, "Failed to count the tokens of a message to summarize. Estimating them instead.");
            return new(text, EstimateTokens(text));
        }
    }

    internal static int EstimateTokens(string text) => (text.Length + 2) / 3;

    /// <summary>
    /// Reads the messages to summarize as the AI received them in the chat, with the text of their documents and transcripts.
    /// </summary>
    /// <remarks>
    /// The images stay out, see RunAsync; the rendered message names them. Reading works on a
    /// copy of each message, so the chat stays untouched. What the filter takes out of the
    /// documents is reported once for all of them, as for a request of the chat, see
    /// ListContentBlockExtensions.BuildMessagesAsync.<br/><br/>
    ///
    /// An earlier answer of the AI starts with the note of AI Studio about the tools it called and
    /// the sources it used, as in every request of the chat, see ToolCallHistoryNote.TextForModel.
    /// The results of those calls are not stored, so the note is all the summary can learn about
    /// which pages the AI read and which searches it ran.
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

            texts[block] = ToolCallHistoryNote.TextForModel(block, await withoutImages.PrepareTextContentForAI(reportingScope));
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
    /// <returns>The summary without the thinking of the model, or an empty string when the provider did not answer.</returns>
    private async Task<string> RequestSummaryAsync(ConversationSummaryRequest request, AIOperationContext context, string summaryRequest, CancellationToken token)
    {
        var answer = await this.AskAsync(request, context, ConversationSummaryPrompts.SYSTEM_PROMPT, summaryRequest, token, request.SourceChat.CaptureRestrictions());
        return answer?.RemoveThinkTags().Trim() ?? string.Empty;
    }
}