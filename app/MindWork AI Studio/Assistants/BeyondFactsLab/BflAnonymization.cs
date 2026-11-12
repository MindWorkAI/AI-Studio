namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A text after the anonymizer of the BeyondFacts Lab ran over it, with what it replaced.
/// </summary>
/// <param name="Text">The anonymized text.</param>
/// <param name="Replacements">What the anonymizer replaced, in the order it first found each.</param>
public sealed record BflAnonymization(string Text, IReadOnlyList<BflReplacement> Replacements)
{
    /// <summary>
    /// Gets the result of a later run over the text, e.g., after the person edited it, together with what this run
    /// replaced already.
    /// </summary>
    /// <param name="later">The later run.</param>
    /// <returns>The text of the later run and the replacements of both runs, counted together.</returns>
    public BflAnonymization CombinedWith(BflAnonymization later)
    {
        var combined = this.Replacements.ToList();
        foreach (var replacement in later.Replacements)
        {
            var index = combined.FindIndex(candidate => candidate.Placeholder == replacement.Placeholder && candidate.Original == replacement.Original);
            if (index < 0)
                combined.Add(replacement);
            else
                combined[index] = combined[index] with { Count = combined[index].Count + replacement.Count };
        }

        return new BflAnonymization(later.Text, combined);
    }
}