namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A value which the person adds to the card sort of the BeyondFacts Lab because the deck misses it, with what it
/// means to them.
/// </summary>
public sealed class BflOwnValue
{
    /// <summary>
    /// Gets or sets the ID of the value, which the choice of values to look at more closely refers to. It never changes.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the value.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what the value means to the person; it may be empty.
    /// </summary>
    public string Definition { get; set; } = string.Empty;
}