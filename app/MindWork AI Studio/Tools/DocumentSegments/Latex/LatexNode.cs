namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// A part of a LaTeX document, with where it lies in the source.
/// </summary>
/// <param name="Kind">What the part is.</param>
/// <param name="Start">Where the part starts in the source.</param>
/// <param name="End">Where the part ends in the source, exclusive.</param>
internal sealed record LatexNode(LatexNodeKind Kind, int Start, int End)
{
    /// <summary>
    /// The name of a command or an environment, without a star, e.g., "section"; the character of a special one.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The words which escaped markup stands for.
    /// </summary>
    public string Words { get; init; } = string.Empty;

    /// <summary>
    /// The arguments of a command or of the start of an environment, in their order.
    /// </summary>
    public IReadOnlyList<LatexArgument> Arguments { get; init; } = [];

    /// <summary>
    /// The content of a group or of an environment.
    /// </summary>
    public IReadOnlyList<LatexNode> Children { get; init; } = [];
}