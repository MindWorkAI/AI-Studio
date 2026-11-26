namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// An argument of a command or of the start of an environment.
/// </summary>
/// <param name="IsOptional">Whether the argument stands in brackets or parentheses rather than in braces.</param>
/// <param name="ContentStart">Where its content starts in the source, behind the opening bracket or brace.</param>
/// <param name="ContentEnd">Where its content ends in the source, before the closing bracket or brace.</param>
/// <param name="Children">Its content; empty for an argument which LaTeX reads as it stands, e.g., an address.</param>
internal sealed record LatexArgument(bool IsOptional, int ContentStart, int ContentEnd, IReadOnlyList<LatexNode> Children);