using AIStudio.Tools.PowerPoint;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Builds small templates in memory, so that no test depends on the template file of an organization.
/// </summary>
internal static class TestTemplate
{
    public const long SLIDE_WIDTH = 12_192_000;
    public const long SLIDE_HEIGHT = 6_858_000;

    public const int MASTER_TITLE_FONT_SIZE = 4400;
    public const int MASTER_BODY_FONT_SIZE = 2800;

    public static readonly SlotBounds MASTER_TITLE_BOUNDS = new(838_200, 365_125, 10_515_600, 1_325_563);
    public static readonly SlotBounds MASTER_BODY_BOUNDS = new(838_200, 1_825_625, 10_515_600, 4_351_338);

    /// <summary>
    /// Writes a presentation with one slide master and the given layouts.
    /// </summary>
    /// <param name="layouts">The layouts, in the order the master lists them.</param>
    /// <returns>The file, positioned at its start.</returns>
    public static MemoryStream Create(params TestLayout[] layouts)
    {
        var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            var masterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");

            var layoutIds = new P.SlideLayoutIdList();
            for (var index = 0; index < layouts.Length; index++)
            {
                var relationshipId = $"rId{index + 2}";
                var layoutPart = masterPart.AddNewPart<SlideLayoutPart>(relationshipId);
                layoutPart.SlideLayout = new P.SlideLayout(
                    new P.CommonSlideData(ShapeTreeOf(layouts[index].Placeholders)) { Name = layouts[index].Name },
                    new P.ColorMapOverride(new A.MasterColorMapping()));

                layoutPart.AddPart(masterPart);
                layoutIds.Append(new P.SlideLayoutId { Id = 2_147_483_649U + (uint)index, RelationshipId = relationshipId });
            }

            masterPart.SlideMaster = new P.SlideMaster(
                new P.CommonSlideData(ShapeTreeOf([
                    new TestPlaceholder(P.PlaceholderValues.Title, null, MASTER_TITLE_BOUNDS),
                    new TestPlaceholder(P.PlaceholderValues.Body, 1, MASTER_BODY_BOUNDS),
                ])),
                layoutIds,
                new P.TextStyles(
                    new P.TitleStyle(new A.Level1ParagraphProperties(new A.DefaultRunProperties { FontSize = MASTER_TITLE_FONT_SIZE })),
                    new P.BodyStyle(new A.Level1ParagraphProperties(new A.DefaultRunProperties { FontSize = MASTER_BODY_FONT_SIZE })),
                    new P.OtherStyle()));

            presentationPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = 2_147_483_648U, RelationshipId = "rId1" }),
                new P.SlideSize { Cx = (int)SLIDE_WIDTH, Cy = (int)SLIDE_HEIGHT },
                new P.NotesSize { Cx = 6_858_000, Cy = 9_144_000 });
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Writes a presentation with the given layouts and reads it as a template.
    /// </summary>
    public static PresentationTemplate Read(params TestLayout[] layouts)
    {
        using var stream = Create(layouts);
        return PresentationTemplate.Read(stream);
    }

    private static P.ShapeTree ShapeTreeOf(IReadOnlyList<TestPlaceholder> placeholders)
    {
        var shapeTree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()));

        for (var index = 0; index < placeholders.Count; index++)
            shapeTree.Append(ShapeOf((uint)index + 2, placeholders[index]));

        return shapeTree;
    }

    private static P.Shape ShapeOf(uint id, TestPlaceholder placeholder)
    {
        var placeholderShape = new P.PlaceholderShape();
        if (placeholder.Type is { } type)
            placeholderShape.Type = type;

        if (placeholder.Index is { } index)
            placeholderShape.Index = index;

        var shapeProperties = new P.ShapeProperties();
        if (placeholder.Bounds is { } bounds)
            shapeProperties.Transform2D = new A.Transform2D(
                new A.Offset { X = bounds.X, Y = bounds.Y },
                new A.Extents { Cx = bounds.Width, Cy = bounds.Height });

        var listStyle = placeholder.FontSize is { } fontSize
            ? new A.ListStyle(new A.Level1ParagraphProperties(new A.DefaultRunProperties { FontSize = fontSize }))
            : new A.ListStyle();

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Placeholder {id}" },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties(placeholderShape)),
            shapeProperties,
            new P.TextBody(new A.BodyProperties(), listStyle, new A.Paragraph()));
    }
}

/// <summary>
/// A slide layout of a test template.
/// </summary>
internal sealed record TestLayout(string Name, params TestPlaceholder[] Placeholders);

/// <summary>
/// A placeholder of a test layout; without bounds, it takes the place of the master placeholder.
/// </summary>
internal sealed record TestPlaceholder(P.PlaceholderValues? Type, uint? Index = null, SlotBounds? Bounds = null, int? FontSize = null)
{
    public static TestPlaceholder Title() => new(P.PlaceholderValues.Title);

    public static TestPlaceholder Subtitle(SlotBounds bounds) => new(P.PlaceholderValues.SubTitle, 1, bounds);

    /// <summary>
    /// A content placeholder, which the file writes without a type.
    /// </summary>
    public static TestPlaceholder Text(uint index, SlotBounds bounds, int? fontSize = null) => new(null, index, bounds, fontSize);

    public static TestPlaceholder Label(uint index, SlotBounds bounds, int? fontSize = null) => new(P.PlaceholderValues.Body, index, bounds, fontSize);

    public static TestPlaceholder Picture(uint index, SlotBounds bounds) => new(P.PlaceholderValues.Picture, index, bounds);

    public static TestPlaceholder Table(uint index, SlotBounds bounds) => new(P.PlaceholderValues.Table, index, bounds);
}