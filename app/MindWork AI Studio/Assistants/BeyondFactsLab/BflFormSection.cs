namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A section of a form "Your details" of the BeyondFacts Lab, which groups fields under a heading.
/// </summary>
/// <remarks>
/// The step "Values and perspectives" follows the three sections of Lara Wüster's dialogue logic. The forms of the
/// other steps have no sections.
/// </remarks>
public enum BflFormSection
{
    /// <summary>
    /// The field stands in the form without a section.
    /// </summary>
    NONE,

    /// <summary>
    /// What matters to the person, with the card sort.
    /// </summary>
    WHAT_MATTERS_TO_ME,

    /// <summary>
    /// What touches the person personally.
    /// </summary>
    WHAT_TOUCHES_ME,

    /// <summary>
    /// What moves others, whose points of view the person has hardly considered so far.
    /// </summary>
    WHAT_MOVES_OTHERS,
}