using System.Text;
using System.Xml;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tools.PowerPoint;

internal static partial class PresentationWriter
{
    /// <summary>
    /// A picture slot covering at least this share of the slide is a backdrop, which the picture fills.
    /// </summary>
    /// <remarks>
    /// A backdrop, such as the picture of a chapter slide, looks broken with a margin, so the picture
    /// is cropped to fill it. A smaller slot often gets a diagram or a screenshot, which must not lose
    /// its edges, so the picture is scaled to fit inside and keeps all of its content.
    /// </remarks>
    private const double BACKDROP_AREA_SHARE = 0.5;

    /// <summary>
    /// A crop of the whole picture, in the thousandths of a percent the file format uses.
    /// </summary>
    private const double WHOLE_PICTURE = 100_000;

    /// <summary>
    /// The height of a table row, 0.4 inch. PowerPoint grows a row whose text needs more.
    /// </summary>
    private const long TABLE_ROW_HEIGHT = 370_840;

    private const string TABLE_GRAPHIC_DATA_URI = "http://schemas.openxmlformats.org/drawingml/2006/table";

    /// <summary>
    /// Any fixed ID will do for the field: PowerPoint fills in the number when it shows the slide.
    /// </summary>
    private const string SLIDE_NUMBER_FIELD_ID = "{B6F15528-21DE-4FAA-801E-634DDDAF4B2B}";

    private static P.ShapeTree ShapeTreeOf(SlidePart slidePart, SlideContent content)
    {
        var placeholders = LayoutPlaceholders.Of(content.LayoutElement);
        var shapeTree = NewShapeTree();
        var slide = content.Slide;
        var nextId = 2U;
        foreach (var slot in content.Layout.Slots)
        {
            var index = slot.Ordinal - 1;
            var placeholder = placeholders.For(slot);
            OpenXmlElement? shape = slot.Role switch
            {
                PlaceholderRole.TITLE when slide.Title.Length > 0 => TextShape(nextId, $"Title {nextId}", placeholder, ParagraphsOf(slide.Title), false),
                PlaceholderRole.SUBTITLE when slide.Subtitle.Length > 0 => TextShape(nextId, $"Subtitle {nextId}", placeholder, ParagraphsOf(slide.Subtitle), false),
                PlaceholderRole.TEXT when index < slide.Texts.Count && slide.Texts[index].Paragraphs.Count > 0 => TextShape(nextId, $"Text {nextId}", placeholder, slide.Texts[index].Paragraphs.Select(ParagraphOf), true),
                PlaceholderRole.LABEL when index < slide.Labels.Count && slide.Labels[index].Length > 0 => TextShape(nextId, $"Label {nextId}", placeholder, ParagraphsOf(slide.Labels[index]), false),
                PlaceholderRole.PICTURE when index < slide.Pictures.Count && content.Pictures.TryGetValue(slide.Pictures[index], out var picture) => PictureShape(nextId, placeholder, slidePart, picture, slot.Bounds, slot.Bounds.Area >= content.SlideArea * BACKDROP_AREA_SHARE),
                PlaceholderRole.PICTURE when content.KeepEmptyPicturePlaceholders => EmptyPlaceholderShape(nextId, $"Picture Placeholder {nextId}", placeholder),
                PlaceholderRole.TABLE when index == 0 && slide.Table is { Headers.Count: > 0 } table => TableShape(nextId, placeholder, table, slot.Bounds, content.TableStyleId),

                _ => null,
            };

            if (shape is null)
                continue;

            shapeTree.Append(shape);
            nextId++;
        }

        AppendFooterAndSlideNumber(shapeTree, placeholders, content.FooterText, nextId);
        return shapeTree;
    }

    /// <summary>
    /// The shape tree of a slide, with no shapes in it yet.
    /// </summary>
    private static P.ShapeTree NewShapeTree() => new(
        new P.NonVisualGroupShapeProperties(
            new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
            new P.NonVisualGroupShapeDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties()),
        new P.GroupShapeProperties(new A.TransformGroup(
            new A.Offset { X = 0, Y = 0 },
            new A.Extents { Cx = 0, Cy = 0 },
            new A.ChildOffset { X = 0, Y = 0 },
            new A.ChildExtents { Cx = 0, Cy = 0 })));

    /// <summary>
    /// Adds the footer and the slide number, as far as the layout has places for them.
    /// </summary>
    /// <remarks>
    /// Footer and slide number appear only on slides which carry their placeholders, even when the
    /// layout has them.
    /// </remarks>
    /// <param name="shapeTree">The shape tree of the slide.</param>
    /// <param name="placeholders">The placeholders of the slide's layout.</param>
    /// <param name="footerText">The footer text; empty for no footer.</param>
    /// <param name="nextId">The next free shape ID of the slide.</param>
    private static void AppendFooterAndSlideNumber(P.ShapeTree shapeTree, LayoutPlaceholders placeholders, string footerText, uint nextId)
    {
        if (footerText.Length > 0 && placeholders.Footer is { } footer)
        {
            shapeTree.Append(TextShape(nextId, $"Footer {nextId}", Clone(footer), ParagraphsOf(footerText), false));
            nextId++;
        }

        if (placeholders.SlideNumber is { } slideNumber)
            shapeTree.Append(TextShape(nextId, $"Slide Number {nextId}", Clone(slideNumber), [new A.Paragraph(new A.Field(new A.Text("‹#›")) { Id = SLIDE_NUMBER_FIELD_ID, Type = "slidenum" })], false));
    }

    /// <summary>
    /// A text placeholder. It states no place and no formatting, so both come from the layout.
    /// </summary>
    private static P.Shape TextShape(uint id, string name, P.PlaceholderShape placeholder, IEnumerable<A.Paragraph> paragraphs, bool shrinkOnOverflow)
    {
        var textBody = new P.TextBody(
            shrinkOnOverflow ? new A.BodyProperties(new A.NormalAutoFit()) : new A.BodyProperties(),
            new A.ListStyle());

        textBody.Append(paragraphs);
        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(placeholder)),
            new P.ShapeProperties(),
            textBody);
    }

    /// <summary>
    /// A placeholder without content. PowerPoint shows the prompt text of the layout in it while
    /// editing, and nothing in the slide show.
    /// </summary>
    private static P.Shape EmptyPlaceholderShape(uint id, string name, P.PlaceholderShape placeholder) => new(
        new P.NonVisualShapeProperties(
            new P.NonVisualDrawingProperties { Id = id, Name = name },
            new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
            new P.ApplicationNonVisualDrawingProperties(placeholder)),
        new P.ShapeProperties());

    private static P.Picture PictureShape(uint id, P.PlaceholderShape placeholder, SlidePart slidePart, SlidePicture picture, SlotBounds bounds, bool isBackdrop)
    {
        var imagePart = slidePart.AddImagePart(picture.ContentType);
        using (var data = new MemoryStream(picture.Data))
            imagePart.FeedData(data);

        var blipFill = new P.BlipFill(new A.Blip { Embed = slidePart.GetIdOfPart(imagePart) });
        var shapeProperties = new P.ShapeProperties();
        if (isBackdrop)
        {
            // The picture is cropped for these bounds, which a slide of the template may have moved away from the layout's:
            blipFill.Append(CropToFill(picture, bounds));
            shapeProperties.Transform2D = new A.Transform2D(new A.Offset { X = bounds.X, Y = bounds.Y }, new A.Extents { Cx = bounds.Width, Cy = bounds.Height });
        }
        else
            shapeProperties.Transform2D = FitInside(picture, bounds);

        blipFill.Append(new A.Stretch(new A.FillRectangle()));
        return new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Picture {id}" },
                new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoGrouping = true, NoChangeAspect = true }),
                new P.ApplicationNonVisualDrawingProperties(placeholder)),
            blipFill,
            shapeProperties);
    }

    /// <summary>
    /// Crops the picture evenly on two sides, so that it fills the slot without being distorted.
    /// </summary>
    private static A.SourceRectangle CropToFill(SlidePicture picture, SlotBounds bounds)
    {
        var slotRatio = (double)bounds.Width / bounds.Height;
        var pictureRatio = (double)picture.Width / picture.Height;
        var crop = new A.SourceRectangle();
        if (pictureRatio > slotRatio)
        {
            var side = (int)Math.Round((1 - slotRatio / pictureRatio) / 2 * WHOLE_PICTURE);
            crop.Left = side;
            crop.Right = side;
        }
        else if (pictureRatio < slotRatio)
        {
            var side = (int)Math.Round((1 - pictureRatio / slotRatio) / 2 * WHOLE_PICTURE);
            crop.Top = side;
            crop.Bottom = side;
        }

        return crop;
    }

    /// <summary>
    /// Scales the picture to the largest size which fits into the slot, centered in it.
    /// </summary>
    private static A.Transform2D FitInside(SlidePicture picture, SlotBounds bounds)
    {
        var scale = Math.Min((double)bounds.Width / picture.Width, (double)bounds.Height / picture.Height);
        var width = (long)Math.Round(picture.Width * scale);
        var height = (long)Math.Round(picture.Height * scale);
        return new A.Transform2D(
            new A.Offset { X = bounds.X + (bounds.Width - width) / 2, Y = bounds.Y + (bounds.Height - height) / 2 },
            new A.Extents { Cx = width, Cy = height });
    }

    /// <summary>
    /// A table in the table slot, in the default table style of the template, with a header row.
    /// </summary>
    private static P.GraphicFrame TableShape(uint id, P.PlaceholderShape placeholder, SlideTableSpec table, SlotBounds bounds, string? tableStyleId)
    {
        var columns = table.Headers.Count;
        var columnWidth = bounds.Width / columns;
        var properties = new A.TableProperties { FirstRow = true, BandRow = true };
        if (tableStyleId is not null)
            properties.Append(new A.TableStyleId(tableStyleId));

        var grid = new A.TableGrid();
        for (var column = 0; column < columns; column++)
        {
            // The last column takes what the division left over, so the table spans the slot exactly:
            grid.Append(new A.GridColumn { Width = column < columns - 1 ? columnWidth : bounds.Width - columnWidth * (columns - 1) });
        }

        var drawingTable = new A.Table(properties, grid);
        drawingTable.Append(TableRowOf(table.Headers, columns));
        foreach (var row in table.Rows)
            drawingTable.Append(TableRowOf(row, columns));

        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Table {id}" },
                new P.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(placeholder)),
            new P.Transform(
                new A.Offset { X = bounds.X, Y = bounds.Y },
                new A.Extents { Cx = bounds.Width, Cy = TABLE_ROW_HEIGHT * (table.Rows.Count + 1) }),
            new A.Graphic(new A.GraphicData(drawingTable) { Uri = TABLE_GRAPHIC_DATA_URI }));
    }

    private static A.TableRow TableRowOf(IReadOnlyList<string> cells, int columns)
    {
        var row = new A.TableRow { Height = TABLE_ROW_HEIGHT };
        for (var column = 0; column < columns; column++)
        {
            var text = column < cells.Count ? XmlSafe(cells[column]) : string.Empty;
            var paragraph = text.Length > 0 ? new A.Paragraph(new A.Run(new A.Text(text))) : new A.Paragraph();
            row.Append(new A.TableCell(new A.TextBody(new A.BodyProperties(), new A.ListStyle(), paragraph), new A.TableCellProperties()));
        }

        return row;
    }

    private static A.Paragraph ParagraphOf(SlideParagraph paragraph)
    {
        var result = new A.Paragraph();
        if (paragraph.Level > 0)
            result.Append(new A.ParagraphProperties { Level = paragraph.Level });

        result.Append(new A.Run(new A.Text(XmlSafe(paragraph.Text))));
        return result;
    }

    /// <summary>
    /// One paragraph per line of a title, subtitle, label or footer.
    /// </summary>
    private static List<A.Paragraph> ParagraphsOf(string text) => text
        .ReplaceLineEndings("\n")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => ParagraphOf(new SlideParagraph(line, 0)))
        .ToList();

    /// <summary>
    /// Leaves out the characters XML cannot carry, which a model may produce now and then. The file
    /// could not be saved with them.
    /// </summary>
    private static string XmlSafe(string text)
    {
        if (text.All(XmlConvert.IsXmlChar))
            return text;

        var result = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (XmlConvert.IsXmlChar(text[index]))
                result.Append(text[index]);
            else if (index + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[index + 1], text[index]))
                result.Append(text[index]).Append(text[++index]);
        }

        return result.ToString();
    }

    /// <summary>
    /// A copy of a layout placeholder, for a slide to refer to it.
    /// </summary>
    private static P.PlaceholderShape Clone(P.PlaceholderShape placeholder)
    {
        var clone = (P.PlaceholderShape)placeholder.CloneNode(true);

        // A prompt of its own means something on a layout only:
        clone.HasCustomPrompt = null;
        return clone;
    }

    /// <summary>
    /// The placeholders of a layout, which the placeholders of a slide refer to by type and index.
    /// </summary>
    private sealed class LayoutPlaceholders
    {
        private readonly Dictionary<(string Type, uint? Index), P.PlaceholderShape> byTypeAndIndex = new();

        public P.PlaceholderShape? Footer { get; private set; }

        public P.PlaceholderShape? SlideNumber { get; private set; }

        public P.PlaceholderShape? Date { get; private set; }

        public static LayoutPlaceholders Of(P.SlideLayout? layout)
        {
            var placeholders = new LayoutPlaceholders();
            foreach (var placeholder in layout?.CommonSlideData?.ShapeTree?.Descendants<P.PlaceholderShape>() ?? [])
            {
                // The same reading as the template's, so that a slot finds its placeholder again:
                var type = placeholder.Type?.InnerText ?? "obj";
                placeholders.byTypeAndIndex.TryAdd((type, placeholder.Index?.Value), placeholder);
                if (type == "ftr")
                    placeholders.Footer ??= placeholder;
                else if (type == "sldNum")
                    placeholders.SlideNumber ??= placeholder;
                else if (type == "dt")
                    placeholders.Date ??= placeholder;
            }

            return placeholders;
        }

        /// <summary>
        /// The placeholder a slot refers to: a copy of the layout's, or a new one with the same type and index.
        /// </summary>
        public P.PlaceholderShape For(TemplateSlot slot)
        {
            if (this.byTypeAndIndex.TryGetValue((slot.PlaceholderType, slot.PlaceholderIndex), out var placeholder))
                return Clone(placeholder);

            var created = new P.PlaceholderShape();
            if (slot.PlaceholderType != "obj")
                created.Type = new P.PlaceholderValues(slot.PlaceholderType);

            if (slot.PlaceholderIndex is { } index)
                created.Index = index;

            return created;
        }
    }
}