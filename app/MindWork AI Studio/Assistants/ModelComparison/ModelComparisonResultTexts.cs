namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The long explanations of the results of the Model Comparison assistant, which its dialog of all results, the result
/// of a comparison series, and the summary of a request share.
/// </summary>
public static class ModelComparisonResultTexts
{
    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(ModelComparisonResultTexts).Namespace, nameof(ModelComparisonResultTexts));

    /// <summary>
    /// What the dialog of all results shows, and what its filter does.
    /// </summary>
    public static string WhatTheResultsShow => TA("Here you see what all your comparisons say about the models. The Answer Test adds your votes on the answers of two models to the same request, and the Field Test adds your decisions on real tasks. A comparison series of the Field Test counts only once you looked at its result, and only with the tasks you decided before, while you did not know the models yet. Choose above whether the results of both modes count, or those of one of them.");

    /// <summary>
    /// How to read the share of wins, its likely range, and the assessment.
    /// </summary>
    public static string HowToReadTheShares => TA("The share of wins counts only the runs and tasks which one of the models won, not the ties. With few of them, chance plays a large part. Therefore, a model counts as clearly better or clearly worse only when the likely range of its share, at a confidence of 95 percent, lies entirely above or below one half. Many runs of the same request tell you a lot about that request, but little about others, so compare the models on different requests and tasks, too.");

    /// <summary>
    /// What the assessment of a pair of models means.
    /// </summary>
    public static string WhatThePairAssessmentMeans => TA("The columns follow the order in the heading: the first model, both equally good, the second model. The assessment says whether one of the two is clearly better. That is the case only when the likely range of its share of wins, at a confidence of 95 percent, lies entirely above one half; the ties do not count toward the share.");

    /// <summary>
    /// What the columns of the agreement of the judges mean, and what a high agreement tells.
    /// </summary>
    public static string WhatTheAgreementMeans => TA("“Compared” counts the runs and tasks with both your vote and a verdict of the judge, “Same choice” those in which the judge chose the same as you, and “Agreement” their share. The higher it is, the more the judge decides like you, and the more its verdicts tell you where you did not vote yourself.");

    /// <summary>
    /// What the result of a comparison series shows.
    /// </summary>
    public static string WhatTheSeriesResultShows => TA("The result counts only the tasks you decided without knowing the models. It shows how often each model helped you more, whether that is clearly more than chance, and how much effort each model cost you.");

    /// <summary>
    /// What the effort of a model in a comparison series means.
    /// </summary>
    public static string WhatTheEffortMeans => TA("The effort tells you how much each model asked of you until you were done with a task: how many messages you wrote on average, your first one included, and how long you worked with the model. For the time, AI Studio takes the median: half of the tasks took less, half took more, so that a chat left open overnight does not distort the picture. Fewer messages and less time can mean that a model understood you sooner.");

    /// <summary>
    /// What the summary of the runs of one request shows, and what it cannot tell.
    /// </summary>
    public static string WhatTheBatchSummaryShows => TA("Here you see how your votes and the verdicts of the judge turned out for this request. A single request tells you little about how the models do in general: compare them on several requests, and on real tasks in the Field Test, before you choose one. The results of all comparisons put everything together.");
}