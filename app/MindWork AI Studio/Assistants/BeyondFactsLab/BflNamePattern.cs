namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// One way in which a name or a term may appear in a text, which the anonymizer of the BeyondFacts Lab replaces.
/// </summary>
/// <param name="Pattern">The regular expression of this way, without the boundaries around it.</param>
/// <param name="Length">The length of what it stands for, so that the longest names are tried first.</param>
/// <param name="Placeholder">The placeholder which replaces it.</param>
/// <param name="IgnoresCase">Whether it matches in any case, e.g., in an address of a web page.</param>
internal sealed record BflNamePattern(string Pattern, int Length, string Placeholder, bool IgnoresCase);