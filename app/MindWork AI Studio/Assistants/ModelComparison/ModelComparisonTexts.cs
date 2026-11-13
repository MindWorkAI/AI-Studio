namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The long explanations of the Answer Test of the Model Comparison assistant.
/// </summary>
public static class ModelComparisonTexts
{
    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(ModelComparisonTexts).Namespace, nameof(ModelComparisonTexts));

    /// <summary>
    /// How the two models are compared, and what comparing a model with itself shows.
    /// </summary>
    public static string WhatTheModelsAreFor => TA("Choose the two models you want to compare. Both receive the same request, and AI Studio shows their answers side by side, in a random order and without their names: you vote for the better answer blindly and learn only at the end which model wrote which. You may also choose the same model twice. Then the comparison shows how much its answers vary when you ask it the same thing again.");

    /// <summary>
    /// What several runs are good for, and what they cost.
    /// </summary>
    public static string WhatTheRunsAreFor => TA("Every model answers a little differently each time, so a single comparison says little. With several runs, AI Studio asks both models the same request again and again, and you vote on every run blindly. The more runs, the more the result tells you, but each run costs one request per model, and one more with a judge.");

    /// <summary>
    /// What the two models receive, and what they do not, unlike in the chat.
    /// </summary>
    public static string WhatTheModelsReceive => TA("Both models receive your question or task and the material below, and nothing else: no tools, no data sources, no mailboxes, and no web pages, even when you use them in the chat. A web address in your request stays plain text, which no model opens. That way, the answers show what the models themselves make of the same text. To compare how well two models help you with tools, data sources, and your own files, use the Field Test.");

    /// <summary>
    /// What the material is, and where it comes from.
    /// </summary>
    public static string WhatTheMaterialIs => TA("The material is a text which both models work on, e.g., a document to summarize, an email to answer, or code to review. Paste it, or load it from a file, e.g., from a PDF document, whose text AI Studio then reads. Leave it empty when your question stands on its own.");

    /// <summary>
    /// What the judge does, why it is useful, and what it costs.
    /// </summary>
    public static string WhatTheJudgeIsFor => TA("A judge is a third model which reads the request and both answers of each run and decides which answer is better, just as blindly as you do. It gives you a second opinion which does not depend on your own taste, and it pays attention to what you name below. At the end, you see how often the judge agreed with you: when it rarely does, your votes or your criteria deserve a second look. You see its verdicts only after your own votes, and it costs one more request per run.");
}