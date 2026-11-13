namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Something a slide of a template carries besides the placeholders of its layout, e.g., a picture
/// or a text box its author placed by hand.
/// </summary>
/// <remarks>
/// The model knows a template slide by the slots of its layout, which it fills. Everything else on
/// the slide stays unless the model removes it, so the model learns what there is and where:
/// a sample picture, a hint to name the source of a picture, or a chart in the way of a slot.
/// </remarks>
/// <param name="Number">The number of the element on its slide, starting at 1, from back to front; the model refers to it as "element 1".</param>
/// <param name="ShapeId">The ID of the shape on the slide, which finds the element again when the presentation is written.</param>
/// <param name="Kind">What kind of element it is.</param>
/// <param name="Bounds">Where it sits on the slide; null when the file does not say.</param>
/// <param name="Text">Its text, one paragraph per line; empty when it has none.</param>
internal sealed record TemplateSlideElement(int Number, uint ShapeId, TemplateSlideElementKind Kind, SlotBounds? Bounds, string Text)
{
    /// <summary>
    /// How the model refers to the element.
    /// </summary>
    public string Name => $"element {this.Number}";
}