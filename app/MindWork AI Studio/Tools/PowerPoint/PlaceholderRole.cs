namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What a placeholder of a slide layout is for, as far as filling it is concerned.
/// </summary>
/// <remarks>
/// Templates rarely say what a layout is for: most of them leave the layout type at "custom". So the
/// role is read from the placeholder itself, its type and its size, and a text placeholder too small
/// to hold more than a line becomes a label rather than a text.
/// </remarks>
internal enum PlaceholderRole
{
    /// <summary>
    /// A placeholder we cannot fill, e.g., a chart or a media placeholder.
    /// </summary>
    UNSUPPORTED = 0,

    TITLE,
    SUBTITLE,

    /// <summary>
    /// A text area large enough for paragraphs or bullet points.
    /// </summary>
    TEXT,

    /// <summary>
    /// A small text, e.g., a heading above a text area, the source of a picture, or a chapter number.
    /// </summary>
    LABEL,

    PICTURE,
    TABLE,

    FOOTER,
    SLIDE_NUMBER,
    DATE,
}