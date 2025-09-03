namespace AIStudio.Provider.Perplexity;

/// <summary>
/// The delta text of a choice.
/// </summary>
/// <param name="Content">The content of the delta text.</param>
public readonly record struct Delta(string Content);