namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What the lab read from the answer in which the model wrote a work state, see BflMarkdownSections.
/// </summary>
public sealed class BflWorkStateAnswer
{
    /// <summary>
    /// Gets the texts of the sections of the work state, by their keys: the first one of each known section.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Sections { get; init; }

    /// <summary>
    /// Gets the values the model suggests for empty fields of the form of the step, by the IDs of the fields. A
    /// suggestion which does not fit its field is left out.
    /// </summary>
    public required IReadOnlyDictionary<string, string> FieldSuggestions { get; init; }

    /// <summary>
    /// Gets what is wrong with the sections of the answer, in German for the model, which may correct them once.
    /// </summary>
    public required IReadOnlyList<string> Problems { get; init; }
}