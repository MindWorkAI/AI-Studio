namespace AIStudio.Models.Matching;

/// <summary>
/// What a rule does once it matches.
/// </summary>
public enum ModelRuleKind
{
    /// <summary>
    /// Chooses which model this is. Exactly one selector wins, the most specific one.
    /// </summary>
    SELECTOR,

    /// <summary>
    /// Adjusts whatever the selector chose. Every matching modifier applies.
    /// </summary>
    /// <remarks>
    /// This is for the statements which hold across families, and which every family would
    /// otherwise have to repeat: a base checkpoint was never instruction tuned no matter who built
    /// it, and a gateway serving somebody else's model cannot offer that vendor's own API. In the
    /// old rules those had to sit at the very top of the file, which is why anything below them
    /// could not state an exception.
    /// </remarks>
    MODIFIER,
}