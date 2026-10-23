namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What the model reads about the kinds of slides the user chose layouts for.
/// </summary>
internal static class LayoutPurposeExtensions
{
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