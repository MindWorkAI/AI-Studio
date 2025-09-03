namespace AIStudio.Provider.Perplexity;

/// <summary>
/// Data model for a choice made by the AI.
/// </summary>
/// <param name="Index">The index of the choice.</param>
/// <param name="Delta">The delta text of the choice.</param>
public readonly record struct Choice(int Index, Delta Delta);