namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// A kind of slide for which the user may choose the layout of the template.
/// </summary>
/// <remarks>
/// A choice says which layout a kind of slide uses, not whether the presentation has such a slide
/// at all. The members follow the course of a presentation, which is the order the form lists them
/// in. Public, because the settings store the choices by these members.
/// </remarks>
public enum LayoutPurpose
{
    /// <summary>
    /// The title slide, which opens the presentation.
    /// </summary>
    TITLE,

    /// <summary>
    /// An agenda, or an overview of the parts of the presentation.
    /// </summary>
    AGENDA,

    /// <summary>
    /// The slide which opens a chapter.
    /// </summary>
    CHAPTER,

    /// <summary>
    /// An ordinary slide with content.
    /// </summary>
    CONTENT,

    /// <summary>
    /// A slide with little content, e.g., a single statement, a quote, or a key figure.
    /// </summary>
    LITTLE_CONTENT,

    /// <summary>
    /// A slide with much content, e.g., many points or a longer explanation.
    /// </summary>
    MUCH_CONTENT,

    /// <summary>
    /// A slide which presents pictures, e.g., screenshots, photos, or sketches.
    /// </summary>
    MEDIA,

    /// <summary>
    /// A comparison, e.g., before and after, or pros and cons.
    /// </summary>
    COMPARISON,

    /// <summary>
    /// A timeline, e.g., the course of a project.
    /// </summary>
    TIMELINE,

    /// <summary>
    /// A process, e.g., the steps of a workflow.
    /// </summary>
    PROCESS,

    /// <summary>
    /// A hypothesis.
    /// </summary>
    HYPOTHESIS,

    /// <summary>
    /// A research question.
    /// </summary>
    RESEARCH_QUESTION,

    /// <summary>
    /// Feedback, e.g., of customers or users.
    /// </summary>
    FEEDBACK,

    /// <summary>
    /// Source code.
    /// </summary>
    CODE,

    /// <summary>
    /// A question to the audience during the presentation.
    /// </summary>
    AUDIENCE_QUESTION,

    /// <summary>
    /// A summary or a conclusion.
    /// </summary>
    SUMMARY,

    /// <summary>
    /// The closing slide, e.g., with thanks and contact details.
    /// </summary>
    CLOSING,
}