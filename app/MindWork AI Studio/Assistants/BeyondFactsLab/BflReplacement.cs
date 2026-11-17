namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What the anonymizer of the BeyondFacts Lab replaced in a text, and how often.
/// </summary>
/// <param name="Placeholder">The placeholder which stands in the text now, e.g., "[Person 1]".</param>
/// <param name="Original">What stood there before, as it was written.</param>
/// <param name="Count">How often it was replaced.</param>
public sealed record BflReplacement(string Placeholder, string Original, int Count);