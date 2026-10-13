using System.Text;

using AIStudio.Tools.PowerPoint;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using P14 = DocumentFormat.OpenXml.Office2010.PowerPoint;

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
    /// PowerPoint's own default table style, which the test template names as its default.
    /// </summary>
    public const string DEFAULT_TABLE_STYLE_ID = "{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}";

    /// <summary>
    /// The smallest theme the schema allows: PowerPoint opens no presentation without one.
    /// </summary>
    private const string THEME_XML = """
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Test">
          <a:themeElements>
            <a:clrScheme name="Test">
              <a:dk1><a:srgbClr val="000000"/></a:dk1>
              <a:lt1><a:srgbClr val="FFFFFF"/></a:lt1>
              <a:dk2><a:srgbClr val="44546A"/></a:dk2>
              <a:lt2><a:srgbClr val="E7E6E6"/></a:lt2>
              <a:accent1><a:srgbClr val="4472C4"/></a:accent1>
              <a:accent2><a:srgbClr val="ED7D31"/></a:accent2>
              <a:accent3><a:srgbClr val="A5A5A5"/></a:accent3>
              <a:accent4><a:srgbClr val="FFC000"/></a:accent4>
              <a:accent5><a:srgbClr val="5B9BD5"/></a:accent5>
              <a:accent6><a:srgbClr val="70AD47"/></a:accent6>
              <a:hlink><a:srgbClr val="0563C1"/></a:hlink>
              <a:folHlink><a:srgbClr val="954F72"/></a:folHlink>
            </a:clrScheme>
            <a:fontScheme name="Test">
              <a:majorFont><a:latin typeface="Arial"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>
              <a:minorFont><a:latin typeface="Arial"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont>
            </a:fontScheme>
            <a:fmtScheme name="Test">
              <a:fillStyleLst>
                <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
              </a:fillStyleLst>
              <a:lnStyleLst>
                <a:ln w="6350"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln>
                <a:ln w="12700"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln>
                <a:ln w="19050"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln>
              </a:lnStyleLst>
              <a:effectStyleLst>
                <a:effectStyle><a:effectLst/></a:effectStyle>
                <a:effectStyle><a:effectLst/></a:effectStyle>
                <a:effectStyle><a:effectLst/></a:effectStyle>
              </a:effectStyleLst>
              <a:bgFillStyleLst>
                <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
              </a:bgFillStyleLst>
            </a:fmtScheme>
          </a:themeElements>
        </a:theme>
        """;

    private const string SECTION_LIST_URI = "{521415D9-36F7-43E2-AB2F-B90AF26B5E84}";

    /// <summary>
    /// Writes a presentation with one slide master and the given layouts.
    /// </summary>
    /// <param name="layouts">The layouts, in the order the master lists them.</param>
    /// <returns>The file, positioned at its start.</returns>
    public static MemoryStream Create(params TestLayout[] layouts) => Create(new TestTemplateOptions(), layouts);

    /// <summary>
    /// Writes a presentation with one slide master and the given layouts.
    /// </summary>
    /// <param name="options">What else the file holds, and whether it is a template (.potx).</param>
    /// <param name="layouts">The layouts, in the order the master lists them.</param>
    /// <returns>The file, positioned at its start.</returns>
    public static MemoryStream Create(TestTemplateOptions options, params TestLayout[] layouts)
    {
        var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, options.AsTemplate ? PresentationDocumentType.Template : PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            var masterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
            var themePart = masterPart.AddNewPart<ThemePart>($"rId{layouts.Length + 2}");
            using (var theme = new MemoryStream(Encoding.UTF8.GetBytes(THEME_XML)))
                themePart.FeedData(theme);

            var tableStylesPart = presentationPart.AddNewPart<TableStylesPart>("rId2");
            tableStylesPart.TableStyleList = new A.TableStyleList { Default = DEFAULT_TABLE_STYLE_ID };

            var layoutParts = new List<SlideLayoutPart>();
            var layoutIds = new P.SlideLayoutIdList();
            for (var index = 0; index < layouts.Length; index++)
            {
                var relationshipId = $"rId{index + 2}";
                var layoutPart = masterPart.AddNewPart<SlideLayoutPart>(relationshipId);
                layoutPart.SlideLayout = new P.SlideLayout(
                    new P.CommonSlideData(ShapeTreeOf(layouts[index].Placeholders)) { Name = layouts[index].Name },
                    new P.ColorMapOverride(new A.MasterColorMapping()));

                layoutPart.AddPart(masterPart);
                layoutParts.Add(layoutPart);
                layoutIds.Append(new P.SlideLayoutId { Id = 2_147_483_649U + (uint)index, RelationshipId = relationshipId });
            }

            masterPart.SlideMaster = new P.SlideMaster(
                new P.CommonSlideData(ShapeTreeOf([
                    new TestPlaceholder(P.PlaceholderValues.Title, null, MASTER_TITLE_BOUNDS),
                    new TestPlaceholder(P.PlaceholderValues.Body, 1, MASTER_BODY_BOUNDS),
                ])),
                new P.ColorMap
                {
                    Background1 = A.ColorSchemeIndexValues.Light1,
                    Text1 = A.ColorSchemeIndexValues.Dark1,
                    Background2 = A.ColorSchemeIndexValues.Light2,
                    Text2 = A.ColorSchemeIndexValues.Dark2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1,
                    Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3,
                    Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5,
                    Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink,
                },
                layoutIds,
                new P.TextStyles(
                    new P.TitleStyle(new A.Level1ParagraphProperties(new A.DefaultRunProperties { FontSize = MASTER_TITLE_FONT_SIZE })),
                    new P.BodyStyle(new A.Level1ParagraphProperties(new A.DefaultRunProperties { FontSize = MASTER_BODY_FONT_SIZE })),
                    new P.OtherStyle()));

            //
            // Templates come with sample slides and hints, grouped into sections, which a
            // presentation written from the template must not keep:
            //
            var slideIds = new P.SlideIdList();
            var sectionSlideIds = new P14.SectionSlideIdList();
            for (var index = 0; index < options.ExampleSlides; index++)
            {
                var relationshipId = $"rId{100 + index}";
                var slidePart = presentationPart.AddNewPart<SlidePart>(relationshipId);
                slidePart.AddPart(layoutParts[0]);
                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(ShapeTreeOf([TestPlaceholder.Title()])),
                    new P.ColorMapOverride(new A.MasterColorMapping()));

                slideIds.Append(new P.SlideId { Id = 256U + (uint)index, RelationshipId = relationshipId });
                sectionSlideIds.Append(new P14.SectionSlideIdListEntry { Id = 256U + (uint)index });
            }

            presentationPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = 2_147_483_648U, RelationshipId = "rId1" }),
                slideIds,
                new P.SlideSize { Cx = (int)SLIDE_WIDTH, Cy = (int)SLIDE_HEIGHT },
                new P.NotesSize { Cx = 6_858_000, Cy = 9_144_000 });

            if (options.ExampleSlides > 0)
            {
                presentationPart.Presentation.PresentationExtensionList = new P.PresentationExtensionList(
                    new P.PresentationExtension(new P14.SectionList(new P14.Section(sectionSlideIds) { Name = "Hints", Id = "{8A7F8F2C-6E59-4C61-9D3E-5B4D2D8F0A11}" })) { Uri = SECTION_LIST_URI });
            }
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
/// What a test template holds beyond its layouts.
/// </summary>
/// <param name="ExampleSlides">How many sample slides the template carries, grouped into one section.</param>
/// <param name="AsTemplate">Whether the file is a template (.potx) rather than a presentation.</param>
internal sealed record TestTemplateOptions(int ExampleSlides = 0, bool AsTemplate = false);

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

    public static TestPlaceholder Footer(uint index, SlotBounds bounds) => new(P.PlaceholderValues.Footer, index, bounds);

    public static TestPlaceholder SlideNumber(uint index, SlotBounds bounds) => new(P.PlaceholderValues.SlideNumber, index, bounds);
}