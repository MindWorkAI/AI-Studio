namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// How important a value of the card sort of the BeyondFacts Lab is to the person, for the occasion they prepare.
/// </summary>
/// <remarks>
/// The names are stored, so they never change. A value without a category stays open: it does not count as less
/// important.
/// </remarks>
public enum BflValueCategory
{
    /// <summary>
    /// The person has not sorted the value. It is never stored.
    /// </summary>
    NONE,

    /// <summary>
    /// The value matters especially to the person.
    /// </summary>
    ESPECIALLY_IMPORTANT,

    /// <summary>
    /// The value matters to the person as well.
    /// </summary>
    ALSO_IMPORTANT,

    /// <summary>
    /// The value matters less for this occasion.
    /// </summary>
    LESS_IMPORTANT,

    /// <summary>
    /// The person is unsure about the value.
    /// </summary>
    UNCLEAR,
}