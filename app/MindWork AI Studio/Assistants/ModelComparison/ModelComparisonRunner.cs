using System.Text;

using AIStudio.Chat;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Sends one request to two models at the same time and measures how each of them does. An optional
/// third model, the judge, is asked about the two answers once both are in.
/// </summary>
/// <remarks>
/// The two requests do not depend on each other. A model which fails, or takes very long, costs
/// the other one nothing, and only a cancellation of the token ends both. Nothing here throws for a
/// model which did not answer: that is a result to report, and the caller has to be able to show it
/// next to the answer of the other model. The judge is different: it needs both answers, so it runs
/// only after them, not alongside them.
///
/// Every await here uses <c>ConfigureAwait(false)</c>: this class is invoked from a Blazor Server
/// page (<c>ModelComparisonBatchRunner</c>, in turn from the assistant itself), and the first of
/// them would otherwise still be running on that page's own synchronisation context -- caught before
/// its own first suspension, the same way <c>ModelComparisonBatchRunner.RunOneAsync</c> is. Without
/// this, every chunk streamed from a model, the judge included, would try to resume on that same
/// context, the very thing a click on the page also has to wait its turn on.
/// </remarks>
/// <param name="logger">Where to say why a model did not answer. The users see no reason: the text of an error may name a provider.</param>
/// <param name="filterJudgeInputs">
/// Filters the two answers, or the two chats, before the judge reads them, and gives them back in the same order. In
/// the app, this is the prompt injection filter, see ModelComparisonProviderAdapter.CreateJudgeInputFilter.
/// </param>
/// <param name="timeProvider">The clock which measures the times. The system clock, unless a test brings its own.</param>
public sealed class ModelComparisonRunner(ILogger logger, Func<IReadOnlyList<string>, Task<IReadOnlyList<string>>> filterJudgeInputs, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;

    /// <param name="first">The first model.</param>
    /// <param name="second">The second model.</param>
    /// <param name="request">What both models are asked.</param>
    /// <param name="token">Cancels the run, first model, second model, and judge alike.</param>
    /// <param name="presentationOrder">
    /// Which of the two models will stand in column A once the answers are shown. The judge is told
    /// the answers as "Answer A" and "Answer B", the same terms the user sees, so this has to be
    /// settled before it is asked rather than after. Ignored without a <paramref name="judge"/>.
    /// </param>
    /// <param name="judge">The optional judge, asked once both models have answered.</param>
    /// <param name="judgeInstructions">What the user wants the judge to pay attention to. Ignored without a <paramref name="judge"/>.</param>
    public async Task<ModelComparisonRunResult> RunAsync(ModelComparisonParticipant first, ModelComparisonParticipant second, ModelComparisonRequest request, CancellationToken token, ModelComparisonPresentationOrder presentationOrder = ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, ModelComparisonParticipant? judge = null, string judgeInstructions = "")
    {
        //
        // A fresh ID for every run, the same one for both models: it stops a caching gateway between
        // here and the models from answering a repeat run out of its cache instead of asking again,
        // without making the two models' prompts differ from each other.
        //
        var prompt = request.ToPrompt(Guid.NewGuid().ToString("N"));

        //
        // Task.Run on purpose: a provider may do work before its first await, and without it the
        // second model would only be asked once the first one had got that far.
        //
        var firstTask = Task.Run(() => this.AskAsync(first, prompt, token), CancellationToken.None);
        var secondTask = Task.Run(() => this.AskAsync(second, prompt, token), CancellationToken.None);
        await Task.WhenAll(firstTask, secondTask).ConfigureAwait(false);

        var firstAnswer = await firstTask.ConfigureAwait(false);
        var secondAnswer = await secondTask.ConfigureAwait(false);

        return new ModelComparisonRunResult
        {
            First = firstAnswer,
            Second = secondAnswer,
            Judge = await this.AskJudgeAsync(judge, judgeInstructions, request, presentationOrder, firstAnswer, secondAnswer, token).ConfigureAwait(false),
        };
    }

    /// <summary>
    /// Asks the judge once both models have answered.
    /// </summary>
    /// <remarks>
    /// The judge needs both complete answers to judge, so it is asked after them, never alongside
    /// them. Nothing here throws: a judge which fails is a verdict which is not completed, the same
    /// as a model which did not answer in AskAsync.
    /// </remarks>
    private async Task<ModelComparisonJudgeVerdict?> AskJudgeAsync(ModelComparisonParticipant? judge, string judgeInstructions, ModelComparisonRequest request, ModelComparisonPresentationOrder presentationOrder, ModelComparisonAnswer first, ModelComparisonAnswer second, CancellationToken token)
    {
        if (judge is null || !first.Completed || !second.Completed || token.IsCancellationRequested)
            return null;

        var (answerA, answerB) = presentationOrder is ModelComparisonPresentationOrder.FIRST_MODEL_FIRST
            ? (first.Text, second.Text)
            : (second.Text, first.Text);

        //
        // Both answers were written by a model, and a model may repeat what it read, a document of the context for
        // example, together with any instructions hidden in it. They reach the judge as part of its prompt, so they
        // pass the prompt injection filter first, the same as an answer which is sent on to another assistant. A
        // judge which cannot get filtered answers is not asked at all:
        //
        if (await this.FilterForJudgeAsync(judge, [answerA, answerB]).ConfigureAwait(false) is not { } filteredAnswers)
            return ModelComparisonJudgeVerdict.Parse(judge.Label, false, string.Empty);

        var judgeRequest = new ModelComparisonJudgeRequest(request, judgeInstructions, filteredAnswers[0], filteredAnswers[1], request.LanguageName);
        var judgeAnswer = await this.AskAsync(judge, judgeRequest.ToPrompt(Guid.NewGuid().ToString("N")), token).ConfigureAwait(false);

        return ModelComparisonJudgeVerdict.Parse(judgeAnswer.Label, judgeAnswer.Completed, judgeAnswer.Text);
    }

    /// <summary>
    /// Asks the judge about the two chats of a task of a comparison series, see ModelComparisonChatJudgeRequest.
    /// </summary>
    /// <remarks>
    /// The chats hold answers of models, so they pass the same filter as the answers of a run before the judge reads
    /// them. Nothing here throws: a judge which fails, or which is canceled, gives a verdict which is not completed.
    /// </remarks>
    /// <param name="judge">The judge.</param>
    /// <param name="transcriptOfChatOne">The first chat, see ChatTranscript.</param>
    /// <param name="transcriptOfChatTwo">The second chat.</param>
    /// <param name="languageName">The language of the reasoning, by its English name, or an empty text to leave it to the judge.</param>
    /// <param name="token">Cancels the judge.</param>
    /// <returns>The verdict: COLUMN_A names the first chat, COLUMN_B the second one.</returns>
    public async Task<ModelComparisonJudgeVerdict> JudgeChatsAsync(ModelComparisonParticipant judge, string transcriptOfChatOne, string transcriptOfChatTwo, string languageName, CancellationToken token)
    {
        if (await this.FilterForJudgeAsync(judge, [transcriptOfChatOne, transcriptOfChatTwo]).ConfigureAwait(false) is not { } filteredChats)
            return ModelComparisonJudgeVerdict.Parse(judge.Label, false, string.Empty, ModelComparisonJudgeFormat.CHATS);

        var judgeRequest = new ModelComparisonChatJudgeRequest(filteredChats[0], filteredChats[1], languageName);
        var judgeAnswer = await this.AskAsync(judge, judgeRequest.ToPrompt(Guid.NewGuid().ToString("N")), token).ConfigureAwait(false);

        return ModelComparisonJudgeVerdict.Parse(judgeAnswer.Label, judgeAnswer.Completed, judgeAnswer.Text, ModelComparisonJudgeFormat.CHATS);
    }

    /// <summary>
    /// Passes what the judge reads through the filter.
    /// </summary>
    /// <param name="judge">The judge, for the log.</param>
    /// <param name="inputs">The two texts the judge compares.</param>
    /// <returns>The filtered texts in the same order, or null when they could not be filtered: then the judge is not asked at all.</returns>
    private async Task<IReadOnlyList<string>?> FilterForJudgeAsync(ModelComparisonParticipant judge, IReadOnlyList<string> inputs)
    {
        try
        {
            return await filterJudgeInputs(inputs).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The texts could not be filtered for the judge '{Label}' of the model comparison.", judge.Label);
            return null;
        }
    }

    /// <summary>
    /// Asks one model. Never throws: whatever goes wrong leaves the answer not completed.
    /// </summary>
    private async Task<ModelComparisonAnswer> AskAsync(ModelComparisonParticipant participant, string prompt, CancellationToken token)
    {
        var text = new StringBuilder();
        var completed = true;
        TimeSpan? firstTokenTime = null;
        var start = this.time.GetTimestamp();

        try
        {
            await foreach (var chunk in participant.Ask(prompt, token).WithCancellation(token).ConfigureAwait(false))
            {
                // A model which is asked to stop may still hand over a last chunk:
                if (token.IsCancellationRequested)
                    break;

                if (chunk.Length == 0)
                    continue;

                firstTokenTime ??= this.time.GetElapsedTime(start);
                text.Append(chunk);
            }

            if (token.IsCancellationRequested)
                completed = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            completed = false;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The model '{Label}' did not answer in the model comparison.", participant.Label);
            completed = false;
        }

        var totalTime = this.time.GetElapsedTime(start);
        var answer = text.ToString().RemoveThinkTags().Trim();

        //
        // A stream which ends cleanly without saying anything is a failure, too. So is one which
        // ends inside the thinking of the model: nothing of what is left is an answer. Counted
        // as answers, both would go into a vote as an empty text -- and always lose it.
        //
        if (completed && answer.Length == 0)
        {
            logger.LogWarning("The model '{Label}' ended without an answer in the model comparison.", participant.Label);
            completed = false;
        }

        return new ModelComparisonAnswer
        {
            Label = participant.Label,
            Text = answer,
            Completed = completed,
            FirstTokenTime = firstTokenTime,
            TotalTime = completed ? totalTime : null,
        };
    }
}