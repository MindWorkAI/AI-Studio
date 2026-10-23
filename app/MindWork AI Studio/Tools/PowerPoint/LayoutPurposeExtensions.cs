namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What the user and the model read about the kinds of slides the user may choose layouts for.
/// </summary>
internal static class LayoutPurposeExtensions
{
    private static string TB(string fallbackEN) => PluginSystem.I18N.I.T(fallbackEN, typeof(LayoutPurposeExtensions).Namespace, nameof(LayoutPurposeExtensions));

    /// <summary>
    /// What the forms offer for a kind of slide the user leaves to the LLM.
    /// </summary>
    public static string NoPreference => TB("No preference (the LLM decides)");

    /// <summary>
    /// The short name of the choice, e.g., "Layout for chapters".
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    public static string Label(this LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.TITLE => TB("Layout for the title slide"),
        LayoutPurpose.AGENDA => TB("Layout for the agenda"),
        LayoutPurpose.CHAPTER => TB("Layout for chapters"),
        LayoutPurpose.CONTENT => TB("Default layout for content"),
        LayoutPurpose.LITTLE_CONTENT => TB("Layout for little content"),
        LayoutPurpose.MUCH_CONTENT => TB("Layout for much content"),
        LayoutPurpose.MEDIA => TB("Layout for media"),
        LayoutPurpose.COMPARISON => TB("Layout for comparisons"),
        LayoutPurpose.TIMELINE => TB("Layout for timelines"),
        LayoutPurpose.PROCESS => TB("Layout for processes"),
        LayoutPurpose.HYPOTHESIS => TB("Layout for hypotheses"),
        LayoutPurpose.RESEARCH_QUESTION => TB("Layout for research questions"),
        LayoutPurpose.FEEDBACK => TB("Layout for feedback"),
        LayoutPurpose.CODE => TB("Layout for source code"),
        LayoutPurpose.AUDIENCE_QUESTION => TB("Layout for questions to the audience"),
        LayoutPurpose.SUMMARY => TB("Layout for the summary"),
        LayoutPurpose.CLOSING => TB("Layout for the closing slide"),

        _ => TB("Layout"),
    };

    /// <summary>
    /// The question the choice answers, which says which slides it applies to.
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    public static string Question(this LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.TITLE => TB("Which slide layout should the title slide use?"),
        LayoutPurpose.AGENDA => TB("Which slide layout should an agenda or an overview of the parts use?"),
        LayoutPurpose.CHAPTER => TB("Which slide layout should open each chapter?"),
        LayoutPurpose.CONTENT => TB("Which slide layout should ordinary slides with content use by default?"),
        LayoutPurpose.LITTLE_CONTENT => TB("Which slide layout should slides with little content use, e.g., a single statement, a quote, or a key figure?"),
        LayoutPurpose.MUCH_CONTENT => TB("Which slide layout should slides with much content use, e.g., many points or a longer explanation?"),
        LayoutPurpose.MEDIA => TB("Which slide layout should slides use which present screenshots, photos, or sketches?"),
        LayoutPurpose.COMPARISON => TB("Which slide layout should comparisons use, e.g., before and after, or pros and cons?"),
        LayoutPurpose.TIMELINE => TB("Which slide layout should timelines use, e.g., the course of a project?"),
        LayoutPurpose.PROCESS => TB("Which slide layout should processes use, e.g., the steps of a workflow?"),
        LayoutPurpose.HYPOTHESIS => TB("Which slide layout should hypotheses use?"),
        LayoutPurpose.RESEARCH_QUESTION => TB("Which slide layout should research questions use?"),
        LayoutPurpose.FEEDBACK => TB("Which slide layout should feedback use, e.g., of customers or users?"),
        LayoutPurpose.CODE => TB("Which slide layout should source code use?"),
        LayoutPurpose.AUDIENCE_QUESTION => TB("Which slide layout should questions to the audience during the presentation use?"),
        LayoutPurpose.SUMMARY => TB("Which slide layout should a summary or a conclusion use?"),
        LayoutPurpose.CLOSING => TB("Which slide layout should the closing slide use, e.g., with thanks and contact details?"),

        _ => TB("Which slide layout should these slides use?"),
    };

    /// <summary>
    /// Describes the slides of this kind for the model, so that it knows which of its slides a choice applies to.
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    /// <returns>The description, e.g., "timelines, e.g., the course of a project".</returns>
    public static string PromptDescription(this LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.TITLE => "the title slide, which is the first slide",
        LayoutPurpose.AGENDA => "an agenda or an overview of the parts of the presentation",
        LayoutPurpose.CHAPTER => "each slide which starts a chapter; it opens the chapter, and the content of the chapter follows on the next slides",
        LayoutPurpose.CONTENT => "ordinary slides with content; use another layout only where a slide needs other slots, e.g., for a picture or a table, or where another choice here applies",
        LayoutPurpose.LITTLE_CONTENT => "slides with little content, e.g., a single statement, a quote, or a key figure",
        LayoutPurpose.MUCH_CONTENT => "slides with much content, e.g., many points or a longer explanation",
        LayoutPurpose.MEDIA => "slides which present attached pictures, e.g., screenshots, photos, or sketches",
        LayoutPurpose.COMPARISON => "comparisons, e.g., before and after, or pros and cons",
        LayoutPurpose.TIMELINE => "timelines, e.g., the course of a project",
        LayoutPurpose.PROCESS => "processes, e.g., the steps of a workflow",
        LayoutPurpose.HYPOTHESIS => "hypotheses",
        LayoutPurpose.RESEARCH_QUESTION => "research questions",
        LayoutPurpose.FEEDBACK => "feedback, e.g., of customers or users",
        LayoutPurpose.CODE => "source code",
        LayoutPurpose.AUDIENCE_QUESTION => "questions to the audience during the presentation",
        LayoutPurpose.SUMMARY => "a summary or a conclusion",
        LayoutPurpose.CLOSING => "the closing slide, e.g., with thanks and contact details",

        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "There is no description for this kind of slide."),
    };
}