namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What kind of element a slide of a template carries besides the placeholders of its layout.
/// </summary>
internal enum TemplateSlideElementKind
{
    /// <summary>
    /// A shape with text, e.g., a text box naming the source of a picture.
    /// </summary>
    TEXT_BOX,

    /// <summary>
    /// A shape without text which shows something all the same, e.g., a colored rectangle.
    /// </summary>
    SHAPE,

    /// <summary>
    /// A line or a connector.
    /// </summary>
    LINE,

    PICTURE,
    TABLE,
    CHART,

    /// <summary>
    /// A SmartArt graphic.
    /// </summary>
    DIAGRAM,

    /// <summary>
    /// Anything else, e.g., an embedded object, a video, or an equation.
    /// </summary>
    OBJECT,

    /// <summary>
    /// Shapes grouped into one, which count as one element.
    /// </summary>
    GROUP,
}