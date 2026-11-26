namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// What a part of a tagged text is.
/// </summary>
public enum TaggedTokenKind
{
    /// <summary>
    /// Words, spaces, and punctuation, which a model works on.
    /// </summary>
    TEXT,

    /// <summary>
    /// The opening tag of a pair, such as &lt;1&gt;.
    /// </summary>
    OPEN,

    /// <summary>
    /// The closing tag of a pair, such as &lt;/1&gt;.
    /// </summary>
    CLOSE,

    /// <summary>
    /// A tag of its own, such as &lt;2/&gt;, which stands for something without words.
    /// </summary>
    PLACEHOLDER,
}