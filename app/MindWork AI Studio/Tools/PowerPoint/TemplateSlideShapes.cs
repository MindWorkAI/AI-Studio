using DocumentFormat.OpenXml;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Which shapes of a slide fill which slots of its layout.
/// </summary>
/// <remarks>
/// Reading a template and editing one of its slides ask the same question, so they share the answer:
/// what the model is told a slot holds is what a new content replaces.
/// </remarks>
internal static class TemplateSlideShapes
{
    /// <summary>
    /// Finds the shapes of a slide which fill the slots of its layout.
    /// </summary>
    /// <remarks>
    /// A placeholder fills the slot of its type and index; of two placeholders for one slot, the
    /// first counts. A table slot whose placeholder holds no table, or which the slide leaves out,
    /// takes a table which is no placeholder at all: an author who pastes a table onto the slide,
    /// or whose table lost the link to its placeholder on the way, still means it as the table of
    /// the slide. Such tables fill these table slots in the order of the slide.
    /// </remarks>
    /// <param name="shapeTree">The shape tree of the slide.</param>
    /// <param name="layout">The layout the slide is based on.</param>
    /// <returns>The top-level shape filling each slot; slots the slide leaves out are missing.</returns>
    public static Dictionary<TemplateSlot, OpenXmlElement> SlotShapesOf(P.ShapeTree? shapeTree, TemplateLayout layout)
    {
        var elements = shapeTree?.Elements().ToList() ?? [];
        var shapesBySlot = new Dictionary<TemplateSlot, OpenXmlElement>();
        foreach (var element in elements)
        {
            if (PlaceholderOf(element) is { } placeholder && layout.SlotOf(placeholder.Type?.InnerText ?? "obj", placeholder.Index?.Value) is { } slot)
                shapesBySlot.TryAdd(slot, element);
        }

        var freeTables = new Queue<OpenXmlElement>(elements.Where(element => PlaceholderOf(element) is null && HoldsTable(element)));
        foreach (var slot in layout.Slots.Where(slot => slot.Role is PlaceholderRole.TABLE && !(shapesBySlot.GetValueOrDefault(slot) is { } shape && HoldsTable(shape))))
        {
            if (!freeTables.TryDequeue(out var table))
                break;

            shapesBySlot[slot] = table;
        }

        return shapesBySlot;
    }

    private static bool HoldsTable(OpenXmlElement element) => element is P.GraphicFrame frame && frame.Graphic?.GraphicData?.GetFirstChild<A.Table>() is not null;

    /// <summary>
    /// The ID of a top-level element of a slide: a shape, a picture, a graphic frame, a group, a
    /// connector, or something wrapped for newer versions of PowerPoint.
    /// </summary>
    /// <returns>The ID, or null for anything else, e.g., the properties of the shape tree itself.</returns>
    public static uint? ShapeIdOf(OpenXmlElement element) => element switch
    {
        P.Shape or P.Picture or P.GraphicFrame or P.GroupShape or P.ConnectionShape or AlternateContent => element.Descendants<P.NonVisualDrawingProperties>().FirstOrDefault()?.Id?.Value,

        _ => null,
    };

    /// <summary>
    /// The placeholder a shape, a picture or a table of a slide fills; null for anything else.
    /// </summary>
    public static P.PlaceholderShape? PlaceholderOf(OpenXmlElement element) => element switch
    {
        P.Shape shape => shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape,
        P.Picture picture => picture.NonVisualPictureProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape,
        P.GraphicFrame frame => frame.NonVisualGraphicFrameProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape,

        _ => null,
    };
}