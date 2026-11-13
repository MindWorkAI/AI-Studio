namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The long explanations of the Field Test of the Model Comparison assistant, which its page and the dialog of a new
/// comparison series share.
/// </summary>
public static class ModelComparisonSeriesTexts
{
    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(ModelComparisonSeriesTexts).Namespace, nameof(ModelComparisonSeriesTexts));

    /// <summary>
    /// What a comparison series is.
    /// </summary>
    public static string WhatASeriesIs => TA("A comparison series collects your real tasks over days or weeks. You work on each task with one of the two models, and then on the same task with the other one; the second chat starts with your first message of the first one. Afterward, you decide which chat helped you more. You learn which model was which only when you look at the result of the series.");

    /// <summary>
    /// How the two models of a series are chosen and kept blind.
    /// </summary>
    public static string WhatTheModelsAreFor => TA("Choose the two models you want to compare, e.g., the model you work with every day and a new one. For every task, AI Studio decides at random which model you chat with first, and it hides their names until you look at the result of the series.");

    /// <summary>
    /// What the judge of a series does, why it is useful, and what it needs.
    /// </summary>
    public static string WhatTheJudgeIsFor => TA("After your own vote on a task, a third model reads both chats and gives its own verdict, without knowing which model wrote which chat. It gives you a second opinion which does not depend on your own taste, and in the result of the series, you see how often it agreed with you. Since the judge reads both chats in full, it must be allowed to see your data sources as well. It costs one more request per task.");

    /// <summary>
    /// What both models of a series share, and what decides what the selection offers.
    /// </summary>
    public static string WhatBothModelsShare => TA("Both models work with the same profile, the same tools, and the same data sources, so that the comparison stays fair. AI Studio therefore offers only what each of them may use: the model with the lower confidence decides which tools and data sources you can choose, and the tool selection lists the others with the reason. When both models can search the data sources themselves, they do so with Semantic Search; otherwise, AI Studio searches them with every message for both. In each task, you can attach files to your message, which both models receive; images only when both models can read them.");
}