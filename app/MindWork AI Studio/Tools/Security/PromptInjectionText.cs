namespace AIStudio.Tools.Security;

/// <summary>
/// One piece of external content to filter, together with where it came from.
/// </summary>
/// <remarks>
/// Several texts may share one source: a web page contributes its content, title, description,
/// and authors, and the user cares about the page, not about which of its fields carried the
/// injection. Filtering groups its report by source accordingly.
/// </remarks>
/// <param name="Text">The content to filter.</param>
/// <param name="Source">Where the content came from, for the report shown to the user.</param>
public readonly record struct PromptInjectionText(string Text, PromptInjectionSource Source);