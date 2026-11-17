namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Overrides where the BeyondFacts Lab stores its preparations, for tests.
/// </summary>
public sealed class BflStorageOptions
{
    /// <summary>
    /// Gets or initializes the folder in which the folder beyondFactsLab is created.
    /// </summary>
    public string? DataDirectory { get; init; }
}