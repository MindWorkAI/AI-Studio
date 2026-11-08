namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// How a field of the BeyondFacts Lab takes its value.
/// </summary>
public enum BflFieldKind
{
    /// <summary>
    /// A short text in one line.
    /// </summary>
    SINGLE_LINE,

    /// <summary>
    /// A longer text over several lines.
    /// </summary>
    MULTI_LINE,

    /// <summary>
    /// The address of a web page.
    /// </summary>
    LINK,

    /// <summary>
    /// One of the options of the field.
    /// </summary>
    CHOICE,

    /// <summary>
    /// Any number of the options of the field.
    /// </summary>
    MULTI_CHOICE,
}