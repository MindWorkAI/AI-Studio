namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// What opens and closes a block comment, such as /* and */.
/// </summary>
/// <param name="Open">What opens the comment.</param>
/// <param name="Close">What closes it.</param>
public sealed record CodeDelimiters(string Open, string Close);