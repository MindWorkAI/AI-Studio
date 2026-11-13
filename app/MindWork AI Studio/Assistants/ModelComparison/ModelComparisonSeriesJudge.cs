using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Asks the judge of a comparison series about the two chats of a task, once the user decided.
/// </summary>
/// <remarks>
/// The judge reads both chats as transcripts, see ChatTranscript, after they passed the prompt injection filter, and
/// only when both fit into its context window together: a judge which sees only the beginning of a chat would judge
/// something else than the user did. Without a known context window, it is asked anyway, and a provider which refuses
/// the length leaves the task without a verdict.
/// </remarks>
/// <param name="settingsManager">The settings.</param>
/// <param name="guard">The prompt injection filter.</param>
/// <param name="tokenCounter">Counts what the judge would read.</param>
/// <param name="logger">The logger.</param>
public sealed class ModelComparisonSeriesJudge(SettingsManager settingsManager, PromptInjectionGuardService guard, ConversationTokenCounter tokenCounter, ILogger<ModelComparisonSeriesJudge> logger)
{
    /// <summary>
    /// How many tokens of the context window stay free for the answer of the judge.
    /// </summary>
    private const int ANSWER_RESERVE_TOKENS = 1_000;

    /// <summary>
    /// Asks the judge about a decided task and notes its verdict, or why there is none, in the task.
    /// </summary>
    /// <param name="series">The series, with its judge.</param>
    /// <param name="task">The decided task.</param>
    /// <param name="chatOne">The first chat of the task, or null when it cannot be read.</param>
    /// <param name="chatTwo">The second chat of the task, or null when it cannot be read.</param>
    /// <param name="title">The title of the assistant, as the source the report of the filter names.</param>
    /// <param name="token">Cancels the judge; the task stays as it was then.</param>
    public async Task JudgeAsync(ModelComparisonSeries series, ModelComparisonSeriesTask task, ChatThread? chatOne, ChatThread? chatTwo, string title, CancellationToken token)
    {
        if (series.Judge is not { } judgeIdentity)
            return;

        var judgeProvider = ModelComparisonProviderCheck.ProviderAsItWas(settingsManager, series.JudgeProviderId, judgeIdentity);
        if (judgeProvider.UsedLLMProvider is LLMProviders.NONE || ModelComparisonProviderCheck.IssueOf(settingsManager, judgeProvider) is not null)
        {
            task.SkipJudge(ModelComparisonSeriesJudgeStatus.UNAVAILABLE);
            return;
        }

        if (chatOne is null || chatTwo is null)
        {
            task.SkipJudge(ModelComparisonSeriesJudgeStatus.NO_VERDICT);
            return;
        }

        var transcriptOfChatOne = ChatTranscript.Of(chatOne);
        var transcriptOfChatTwo = ChatTranscript.Of(chatTwo);
        var languageName = ModelComparisonFormat.LanguageName;

        // The filter only removes text, so the prompt before it is the longest the judge can get:
        if (!await this.FitsAsync(judgeProvider, new ModelComparisonChatJudgeRequest(transcriptOfChatOne, transcriptOfChatTwo, languageName).ToPrompt(), token))
        {
            task.SkipJudge(ModelComparisonSeriesJudgeStatus.TOO_LONG);
            return;
        }

        // One action per task: the user hears once, when the judge is done, what the filter removed from the chats.
        await using var reportingScope = guard.BeginAction();
        var runner = new ModelComparisonRunner(logger, ModelComparisonProviderAdapter.CreateJudgeInputFilter(guard, reportingScope, title));
        var judge = ModelComparisonProviderAdapter.CreateParticipant(judgeProvider, settingsManager, Tools.Components.MODEL_COMPARISON_ASSISTANT, title);
        var verdict = await runner.JudgeChatsAsync(judge, transcriptOfChatOne, transcriptOfChatTwo, languageName, token);

        // A canceled judge has no verdict, but it was not the fault of the judge either:
        token.ThrowIfCancellationRequested();
        task.TakeVerdict(verdict);
    }

    /// <summary>
    /// Checks whether a prompt fits into the context window of the judge, with room for its answer.
    /// </summary>
    /// <param name="judgeProvider">The provider of the judge.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when it fits, or when nobody knows.</returns>
    private async Task<bool> FitsAsync(AIStudio.Settings.Provider judgeProvider, string prompt, CancellationToken token)
    {
        var contextWindow = judgeProvider.GetModelProfile().Context;
        if (!contextWindow.IsKnown)
            return true;

        try
        {
            return await tokenCounter.CountTextAsync(judgeProvider, prompt, token) + ANSWER_RESERVE_TOKENS <= contextWindow.DefaultTokens;
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Could not count the tokens of the chats for the judge of a comparison series. Asking the judge anyway.");
            return true;
        }
    }
}