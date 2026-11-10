namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// What the parser and the reader know about a command.
/// </summary>
/// <param name="Role">What the command means for the segments.</param>
/// <param name="MandatoryArguments">How many arguments in braces the command takes.</param>
/// <param name="TakesOptionalArguments">Whether the command takes arguments in brackets before its last argument in braces, or anywhere when it takes none in braces.</param>
/// <param name="TextArgument">Which of the arguments in braces holds the words of a WORDS or HEADING command, counted from 0.</param>
internal sealed record LatexCommand(LatexCommandRole Role, int MandatoryArguments, bool TakesOptionalArguments, int TextArgument = 0)
{
    /// <summary>
    /// A command the reader does not know, e.g., a macro of the document: a tag of its own with every argument
    /// which follows right after it, since such a macro may hold anything.
    /// </summary>
    public static readonly LatexCommand UNKNOWN = new(LatexCommandRole.INLINE, int.MaxValue, true) { IsKnown = false };

    /// <summary>
    /// Whether the reader knows the command. Only the arguments of a known command may follow after white space;
    /// for any other, a group after a space is words of its own.
    /// </summary>
    public bool IsKnown { get; init; } = true;

    /// <summary>
    /// Whether LaTeX reads the first argument in braces as it stands, e.g., the address of \url, in which a percent sign is no comment.
    /// </summary>
    public bool HasRawFirstArgument { get; init; }

    /// <summary>
    /// Whether the command takes an argument in parentheses, e.g., the trimming of \cmidrule(lr).
    /// </summary>
    public bool TakesParenthesizedArgument { get; init; }
}