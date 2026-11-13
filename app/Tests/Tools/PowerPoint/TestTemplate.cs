using System.Text;

using AIStudio.Tools.PowerPoint;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
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

            // A presentation used as a template carries slides worth keeping, after the samples:
            var slides = options.Slides ?? [];
            var contentSectionSlideIds = new P14.SectionSlideIdList();
            for (var index = 0; index < slides.Count; index++)
            {
                var relationshipId = $"rId{200 + index}";
                var slidePart = presentationPart.AddNewPart<SlidePart>(relationshipId);
                slidePart.AddPart(layoutParts[slides[index].Layout]);

                // The elements lie in front of the placeholders, with the IDs after theirs:
                var shapeTree = ShapeTreeOf(slides[index].Texts.Select(text => text.Placeholder).ToList(), slides[index].Texts.Select(text => text.Text).ToList());
                for (var elementIndex = 0; elementIndex < slides[index].Elements.Count; elementIndex++)
                    shapeTree.Append(slides[index].Elements[elementIndex].Create((uint)(slides[index].Texts.Length + 2 + elementIndex), slidePart));

                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(shapeTree),
                    new P.ColorMapOverride(new A.MasterColorMapping()));

                if (slides[index].IsHidden)
                    slidePart.Slide.Show = false;

                var slideId = 256U + (uint)(options.ExampleSlides + index);
                slideIds.Append(new P.SlideId { Id = slideId, RelationshipId = relationshipId });
                contentSectionSlideIds.Append(new P14.SectionSlideIdListEntry { Id = slideId });
            }

            presentationPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = 2_147_483_648U, RelationshipId = "rId1" }),
                slideIds,
                new P.SlideSize { Cx = (int)SLIDE_WIDTH, Cy = (int)SLIDE_HEIGHT },
                new P.NotesSize { Cx = 6_858_000, Cy = 9_144_000 });

            if (options.ExampleSlides > 0)
            {
                var sections = new P14.SectionList(new P14.Section(sectionSlideIds) { Name = "Hints", Id = "{8A7F8F2C-6E59-4C61-9D3E-5B4D2D8F0A11}" });
                if (slides.Count > 0)
                    sections.Append(new P14.Section(contentSectionSlideIds) { Name = "Slides", Id = "{3C1E6B0D-2F4A-4E8B-9C7D-1A5F6E2B8D40}" });

                presentationPart.Presentation.PresentationExtensionList = new P.PresentationExtensionList(
                    new P.PresentationExtension(sections) { Uri = SECTION_LIST_URI });
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

    /// <summary>
    /// The shape tree of a layout, a master or a slide.
    /// </summary>
    /// <param name="placeholders">The placeholders.</param>
    /// <param name="texts">The text of each placeholder, for a slide; a line break starts a new paragraph.</param>
    private static P.ShapeTree ShapeTreeOf(IReadOnlyList<TestPlaceholder> placeholders, IReadOnlyList<string>? texts = null)
    {
        var shapeTree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()));

        for (var index = 0; index < placeholders.Count; index++)
            shapeTree.Append(ShapeOf((uint)index + 2, placeholders[index], texts?[index] ?? string.Empty));

        return shapeTree;
    }

    private static P.Shape ShapeOf(uint id, TestPlaceholder placeholder, string text)
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

        var textBody = new P.TextBody(new A.BodyProperties(), listStyle);
        if (text.Length == 0)
            textBody.Append(new A.Paragraph());
        else
            textBody.Append(text.Split('\n').Select(line => new A.Paragraph(new A.Run(new A.Text(line)))));

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Placeholder {id}" },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties(placeholderShape)),
            shapeProperties,
            textBody);
    }
}

/// <summary>
/// What a test template holds beyond its layouts.
/// </summary>
/// <param name="ExampleSlides">How many sample slides the template carries, grouped into one section.</param>
/// <param name="AsTemplate">Whether the file is a template (.potx) rather than a presentation.</param>
/// <param name="Slides">The slides with content the template carries after the sample slides.</param>
internal sealed record TestTemplateOptions(int ExampleSlides = 0, bool AsTemplate = false, IReadOnlyList<TestSlide>? Slides = null);

/// <summary>
/// A slide of a test template, based on one of its layouts.
/// </summary>
/// <param name="Layout">The position of the layout in the template, starting at 0.</param>
/// <param name="Texts">The placeholders of the slide with their texts; a line break starts a new paragraph. Placeholders without bounds take the place of the layout's.</param>
internal sealed record TestSlide(int Layout, params (TestPlaceholder Placeholder, string Text)[] Texts)
{
    /// <summary>
    /// Whether the slide is hidden in the slide show.
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// The shapes of the slide which are no placeholders, in front of the placeholders.
    /// </summary>
    public IReadOnlyList<TestElement> Elements { get; init; } = [];
}

/// <summary>
/// A shape of a test slide which is no placeholder, e.g., a table an author pasted onto the slide.
/// </summary>
/// <param name="Create">Writes the shape with the given ID for the given slide.</param>
internal sealed record TestElement(Func<uint, SlidePart, OpenXmlElement> Create)
{
    private const long ROW_HEIGHT = 370_840;

    /// <summary>
    /// A table which is no placeholder, in equally wide columns; its first row is the header.
    /// </summary>
    public static TestElement Table(SlotBounds bounds, params string[][] rows) => new((id, _) => TableOf(id, null, bounds, rows));

    /// <summary>
    /// A table which fills the table placeholder of the given index, as PowerPoint writes one.
    /// </summary>
    public static TestElement TablePlaceholder(uint index, SlotBounds bounds, params string[][] rows) => new((id, _) => TableOf(id, index, bounds, rows));

    private static P.GraphicFrame TableOf(uint id, uint? placeholderIndex, SlotBounds bounds, string[][] rows)
    {
        var columns = rows[0].Length;
        var grid = new A.TableGrid();
        for (var column = 0; column < columns; column++)
            grid.Append(new A.GridColumn { Width = bounds.Width / columns });

        var table = new A.Table(new A.TableProperties { FirstRow = true, BandRow = true }, grid);
        foreach (var row in rows)
        {
            var tableRow = new A.TableRow { Height = ROW_HEIGHT };
            foreach (var cell in row)
                tableRow.Append(new A.TableCell(new A.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text(cell)))), new A.TableCellProperties()));

            table.Append(tableRow);
        }

        var applicationProperties = new P.ApplicationNonVisualDrawingProperties();
        if (placeholderIndex is { } index)
            applicationProperties.PlaceholderShape = new P.PlaceholderShape { Type = P.PlaceholderValues.Table, Index = index };

        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Table {id}" },
                new P.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoGrouping = true }),
                applicationProperties),
            new P.Transform(
                new A.Offset { X = bounds.X, Y = bounds.Y },
                new A.Extents { Cx = bounds.Width, Cy = ROW_HEIGHT * rows.Length }),
            new A.Graphic(new A.GraphicData(table) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }));
    }

    /// <summary>
    /// A text box: a shape with text which is no placeholder. Without text, it shows nothing.
    /// </summary>
    /// <param name="bounds">Where the text box sits.</param>
    /// <param name="text">Its text.</param>
    /// <param name="fill">The color it is filled with, e.g., "4472C4" for a heading copied from a colored placeholder; null for none.</param>
    public static TestElement TextBox(SlotBounds bounds, string text, string? fill = null) => new((id, _) =>
    {
        var properties = new P.ShapeProperties(TransformOf(bounds), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle });
        if (fill is not null)
            properties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = fill }));

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Text box {id}" },
                new P.NonVisualShapeDrawingProperties { TextBox = true },
                new P.ApplicationNonVisualDrawingProperties()),
            properties,
            new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text(text)))));
    });

    /// <summary>
    /// A filled rectangle without text, e.g., a colored area behind a chart.
    /// </summary>
    public static TestElement Rectangle(SlotBounds bounds) => new((id, _) => new P.Shape(
        new P.NonVisualShapeProperties(
            new P.NonVisualDrawingProperties { Id = id, Name = $"Rectangle {id}" },
            new P.NonVisualShapeDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties()),
        new P.ShapeProperties(
            TransformOf(bounds),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
            new A.SolidFill(new A.RgbColorModelHex { Val = "4472C4" })),
        new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph())));

    /// <summary>
    /// A straight line.
    /// </summary>
    public static TestElement Line(SlotBounds bounds) => new((id, _) => new P.ConnectionShape(
        new P.NonVisualConnectionShapeProperties(
            new P.NonVisualDrawingProperties { Id = id, Name = $"Line {id}" },
            new P.NonVisualConnectorShapeDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties()),
        new P.ShapeProperties(
            TransformOf(bounds),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Line },
            new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = "000000" })))));

    /// <summary>
    /// A picture which is no placeholder, with an image part of its own.
    /// </summary>
    public static TestElement Picture(SlotBounds bounds, params byte[] data) => new((id, slidePart) =>
    {
        var imagePart = slidePart.AddImagePart("image/png");
        using (var stream = new MemoryStream(data))
            imagePart.FeedData(stream);

        return new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Picture {id}" },
                new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.BlipFill(new A.Blip { Embed = slidePart.GetIdOfPart(imagePart) }, new A.Stretch(new A.FillRectangle())),
            new P.ShapeProperties(TransformOf(bounds), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
    });

    /// <summary>
    /// A bar chart without data, with a chart part of its own.
    /// </summary>
    public static TestElement Chart(SlotBounds bounds) => new((id, slidePart) =>
    {
        var chartPart = slidePart.AddNewPart<ChartPart>();
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(CHART_XML)))
            chartPart.FeedData(stream);

        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Chart {id}" },
                new P.NonVisualGraphicFrameDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(
                new A.Offset { X = bounds.X, Y = bounds.Y },
                new A.Extents { Cx = bounds.Width, Cy = bounds.Height }),
            new A.Graphic(new A.GraphicData(new C.ChartReference { Id = slidePart.GetIdOfPart(chartPart) }) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" }));
    });

    /// <summary>
    /// Elements grouped into one, which keep their own places within the group.
    /// </summary>
    public static TestElement Group(SlotBounds bounds, params TestElement[] children) => new((id, slidePart) =>
    {
        var group = new P.GroupShape(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Group {id}" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup(
                new A.Offset { X = bounds.X, Y = bounds.Y },
                new A.Extents { Cx = bounds.Width, Cy = bounds.Height },
                new A.ChildOffset { X = bounds.X, Y = bounds.Y },
                new A.ChildExtents { Cx = bounds.Width, Cy = bounds.Height })));

        // The children take IDs of their own, apart from those of the slide's top-level shapes:
        for (var index = 0; index < children.Length; index++)
            group.Append(children[index].Create(id * 100 + (uint)index, slidePart));

        return group;
    });

    private const string CHART_XML = """
        <c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <c:chart>
            <c:plotArea>
              <c:layout/>
              <c:barChart>
                <c:barDir val="col"/>
                <c:grouping val="clustered"/>
                <c:axId val="1"/>
                <c:axId val="2"/>
              </c:barChart>
              <c:catAx>
                <c:axId val="1"/>
                <c:scaling><c:orientation val="minMax"/></c:scaling>
                <c:delete val="0"/>
                <c:axPos val="b"/>
                <c:crossAx val="2"/>
              </c:catAx>
              <c:valAx>
                <c:axId val="2"/>
                <c:scaling><c:orientation val="minMax"/></c:scaling>
                <c:delete val="0"/>
                <c:axPos val="l"/>
                <c:crossAx val="1"/>
              </c:valAx>
            </c:plotArea>
          </c:chart>
        </c:chartSpace>
        """;

    private static A.Transform2D TransformOf(SlotBounds bounds) => new(
        new A.Offset { X = bounds.X, Y = bounds.Y },
        new A.Extents { Cx = bounds.Width, Cy = bounds.Height });
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

    public static TestPlaceholder Footer(uint index, SlotBounds bounds) => new(P.PlaceholderValues.Footer, index, bounds);

    public static TestPlaceholder SlideNumber(uint index, SlotBounds bounds) => new(P.PlaceholderValues.SlideNumber, index, bounds);
}