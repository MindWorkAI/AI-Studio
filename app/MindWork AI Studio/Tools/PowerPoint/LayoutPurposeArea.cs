namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// An area of kinds of slides, by which the forms group the choices of layouts.
/// </summary>
/// <remarks>
/// The members follow the order in which the forms list the areas, and the kinds of slides in
/// LayoutPurpose follow the same order.
/// </remarks>
public enum LayoutPurposeArea
{
    /// <summary>
    /// The slides which frame a presentation and structure its course, e.g., the title slide, chapters, or the imprint.
    /// </summary>
    FRAMING,

    /// <summary>
    /// Content of every kind of presentation, e.g., a key message, a comparison, or pictures.
    /// </summary>
    GENERAL,

    /// <summary>
    /// Teaching, training, and workshops, e.g., learning objectives or exercises.
    /// </summary>
    TEACHING,

    /// <summary>
    /// Business and projects, e.g., risks, KPIs, or milestones.
    /// </summary>
    BUSINESS,

    /// <summary>
    /// Science and research, e.g., hypotheses, methodology, or results.
    /// </summary>
    SCIENCE,
}