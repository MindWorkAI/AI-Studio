namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A section of an answer of the model, which starts with a heading such as "## [KEY] Title".
/// </summary>
/// <param name="Key">The key between the brackets, as the model wrote it, without spaces around it.</param>
/// <param name="Text">The text below the heading up to the next one, without empty lines around it.</param>
public sealed record BflMarkdownSection(string Key, string Text);