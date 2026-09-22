namespace AIStudio.Models.Matching;

/// <summary>
/// How tightly a pattern is bound to the name it matches.
/// </summary>
/// <remarks>
/// This is the first thing that decides which of two rules wins, and it is ordered by how much the
/// pattern claims to know: naming the whole model says more than naming how the name begins, which
/// says more than naming a part of it, which says more than appearing somewhere inside it.
/// </remarks>
public enum MatchKind
{
    /// <summary>
    /// The pattern is the whole name.
    /// </summary>
    EXACT,

    /// <summary>
    /// The name begins with the pattern, and a name part ends where the pattern ends.
    /// </summary>
    PREFIX,

    /// <summary>
    /// The pattern appears in the name as one or more whole name parts.
    /// </summary>
    /// <remarks>
    /// This is the one to reach for by default. It is what the old rules meant when they said that
    /// a family name counts "only where a name part begins", so that looking for the Yi family does
    /// not answer for every model whose name happens to contain those two letters.
    /// </remarks>
    SEGMENT,

    /// <summary>
    /// The pattern appears anywhere in the name, boundaries or not.
    /// </summary>
    /// <remarks>
    /// The last resort, for the names where a vendor glues things together, such as a version
    /// number sitting inside a name part. It claims the least and therefore loses against every
    /// other kind, which is what keeps it from swallowing families it was never meant for.
    /// </remarks>
    SUBSTRING,
}