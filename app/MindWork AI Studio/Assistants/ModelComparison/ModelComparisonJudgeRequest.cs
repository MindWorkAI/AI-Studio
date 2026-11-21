namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the judge is asked: the same request the two models received, their two answers, and
/// the expectations, values, and goals of the user, when the user described them.
/// </summary>
/// <remarks>
/// The judge is asked once both models have answered, and is kept as blind to which model wrote
/// which answer as the user is: the answers go in as plain text, labeled "Answer A" and "Answer B",
/// never by model name. These are the same letters the user sees, in the same order, so the judge's
/// own words about "Answer A"/"Answer B" line up with what is on screen -- the caller has to settle
/// the presentation order before building this, not after. ModelComparisonVote is what
/// the judge answers with, the same column terms the user's own vote uses.
/// </remarks>
public sealed record ModelComparisonJudgeRequest
{
    /// <param name="request">The request the two models were asked.</param>
    /// <param name="instructions">
    /// The expectations, values, and goals of the user, in their own words, by which the judge
    /// decides, see ModelComparisonJudgeExpectations; or an empty text, when the user confirmed that
    /// the judge may decide by its own standards with nothing but the generic criterion: which answer
    /// better answers the question or completes the task.
    /// </param>
    /// <param name="answerA">The answer standing in column A.</param>
    /// <param name="answerB">The answer standing in column B.</param>
    /// <param name="languageName">
    /// The language the judge is asked to write its reasoning in, by its English name (for example
    /// "German"), or an empty text to leave the language to the judge itself.
    /// </param>
    public ModelComparisonJudgeRequest(ModelComparisonRequest request, string instructions, string answerA, string answerB, string languageName = "")
    {
        this.Request = request;
        this.Instructions = instructions.Trim();
        this.AnswerA = answerA;
        this.AnswerB = answerB;
        this.LanguageName = languageName.Trim();
    }

    public ModelComparisonRequest Request { get; }

    public string Instructions { get; }

    public string AnswerA { get; }

    public string AnswerB { get; }

    public string LanguageName { get; }

    /// <summary>
    /// What the judge is told to look for: the generic criterion, and the expectations, values, and
    /// goals of the user, when the user described them, which come first.
    /// </summary>
    private string Criterion => "Judge which answer better answers the question or better completes the task in the request below." + ModelComparisonJudgeFormat.ExpectationsOfTheUser(this.Instructions);

    /// <summary>
    /// Builds the prompt for the judge model, asking for one JSON object and nothing else.
    /// </summary>
    /// <param name="cacheBuster">
    /// An identifier to append as a plainly labeled, ignorable line, or an empty text to leave the
    /// prompt exactly as asked. See ModelComparisonRequest.ToPrompt for why: the judge
    /// is asked fresh for every run of a batch, same as the two models, and a caching gateway cannot
    /// tell that apart from one identical request repeated unless the prompt says so itself -- most
    /// of all here, where two similar answers can easily make for a near-identical judge prompt too.
    /// </param>
    /// <returns>The user prompt the judge model receives.</returns>
    public string ToPrompt(string cacheBuster = "")
    {
        var prompt = $$"""
                       You judge two AI answers to the same request. You do not know, and must not guess, which model wrote which answer, so judge only by what the answers say.

                       {{this.Criterion}}

                       # Request
                       {{this.Request.ToPrompt(headingLevel: 2)}}

                       # Answer A
                       {{this.AnswerA}}

                       # Answer B
                       {{this.AnswerB}}

                       Decide which answer is better, or whether they are equally good. {{ModelComparisonJudgeFormat.ANSWERS.AnswerInstructions(this.LanguageName)}}
                       """;

        return ModelComparisonJudgeFormat.WithCacheBuster(prompt, cacheBuster);
    }
}
