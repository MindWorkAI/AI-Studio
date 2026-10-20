using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tools.PowerPoint;

internal sealed partial class PresentationTemplate
{
    /// <summary>
    /// The slide size PowerPoint assumes when a file states none: 16:9, 13.333 by 7.5 inches.
    /// </summary>
    private const long DEFAULT_SLIDE_WIDTH = 12_192_000;

    private const long DEFAULT_SLIDE_HEIGHT = 6_858_000;

    /// <summary>
    /// 18 pt, in the hundredths of a point the file uses. PowerPoint's own default.
    /// </summary>
    private const int DEFAULT_FONT_SIZE = 1800;

    private const double EMU_PER_POINT = 12_700;

    /// <summary>
    /// The space a text box keeps free at its left and right edges unless it says otherwise: 0.1 inch.
    /// </summary>
    private const long DEFAULT_HORIZONTAL_INSET = 91_440;

    /// <summary>
    /// The space a text box keeps free at its top and bottom edges unless it says otherwise: 0.05 inch.
    /// </summary>
    private const long DEFAULT_VERTICAL_INSET = 45_720;

    /// <summary>
    /// A line of text takes about 1.2 times the font size.
    /// </summary>
    private const double LINE_HEIGHT_FACTOR = 1.2;

    /// <summary>
    /// An average character takes about half the font size in width.
    /// </summary>
    private const double CHARACTER_WIDTH_FACTOR = 0.5;

    /// <summary>
    /// Text placeholders covering less than this share of the slide are labels.
    /// </summary>
    /// <remarks>
    /// A heading above a text area, the source of a picture or a chapter number cover one or two
    /// percent of a slide; the smallest real text area, a quarter of a slide split into a grid,
    /// covers more than five.
    /// </remarks>
    private const double LABEL_AREA_SHARE = 0.05;

    /// <summary>
    /// Slots whose top edges lie within one row of this many rows per slide are read as one row.
    /// </summary>
    private const int READING_ROWS = 50;

    /// <summary>
    /// Reads the layouts of a template, and the slides it carries.
    /// </summary>
    /// <remarks>
    /// Works for presentations (.pptx) and templates (.potx) alike. The slides the file contains are
    /// read with what they hold, yet a presentation written later starts without them: in a template
    /// they are mostly samples or hints for the user, and only those the user chooses are kept. The
    /// Open XML SDK throws when the file is no presentation at all.
    /// </remarks>
    /// <param name="stream">The file, opened for reading.</param>
    /// <returns>The template.</returns>
    public static PresentationTemplate Read(Stream stream)
    {
        using var document = PresentationDocument.Open(stream, false);
        var presentationPart = document.PresentationPart ?? throw new InvalidDataException("The file contains no presentation.");
        var presentation = presentationPart.Presentation ?? throw new InvalidDataException("The file contains no presentation.");
        var slideWidth = presentation.SlideSize?.Cx?.Value ?? DEFAULT_SLIDE_WIDTH;
        var slideHeight = presentation.SlideSize?.Cy?.Value ?? DEFAULT_SLIDE_HEIGHT;
        var defaultFontSize = FontSizeOf(presentation.DefaultTextStyle?.Level1ParagraphProperties) ?? DEFAULT_FONT_SIZE;

        var layouts = new List<TemplateLayout>();
        foreach (var masterId in presentation.SlideMasterIdList?.Elements<P.SlideMasterId>() ?? [])
        {
            if (masterId.RelationshipId?.Value is not { } masterRelationshipId || !presentationPart.TryGetPartById(masterRelationshipId, out var masterCandidate) || masterCandidate is not SlideMasterPart { SlideMaster: { } slideMaster } masterPart)
                continue;

            var master = MasterOf(slideMaster, defaultFontSize);
            foreach (var layoutId in slideMaster.SlideLayoutIdList?.Elements<P.SlideLayoutId>() ?? [])
            {
                if (layoutId.RelationshipId?.Value is not { } layoutRelationshipId || !masterPart.TryGetPartById(layoutRelationshipId, out var layoutCandidate) || layoutCandidate is not SlideLayoutPart { SlideLayout: { } slideLayout } layoutPart)
                    continue;

                layouts.Add(ReadLayout(slideLayout, layoutPart.Uri, master, slideWidth, slideHeight));
            }
        }

        var (families, variantLabels) = GroupIntoFamilies(layouts, slideWidth, slideHeight);
        var slides = ReadSlides(presentationPart, presentation, layouts, families, slideWidth, slideHeight);
        return new PresentationTemplate(slideWidth, slideHeight, families, variantLabels, slides);
    }

    private static MasterStyles MasterOf(P.SlideMaster master, int defaultFontSize)
    {
        var placeholders = new Dictionary<string, PlaceholderInfo>(StringComparer.Ordinal);
        foreach (var placeholder in PlaceholdersOf(master.CommonSlideData))
            placeholders.TryAdd(MasterTypeOf(placeholder.Type), placeholder);

        var titleFontSize = FontSizeOf(master.TextStyles?.TitleStyle?.Level1ParagraphProperties) ?? defaultFontSize;
        var bodyFontSize = FontSizeOf(master.TextStyles?.BodyStyle?.Level1ParagraphProperties) ?? defaultFontSize;
        return new MasterStyles(placeholders, titleFontSize, bodyFontSize, defaultFontSize);
    }

    private static TemplateLayout ReadLayout(P.SlideLayout layout, Uri partUri, MasterStyles master, long slideWidth, long slideHeight)
    {
        var slideArea = (double)slideWidth * slideHeight;
        var slots = new List<TemplateSlot>();
        foreach (var placeholder in PlaceholdersOf(layout.CommonSlideData))
        {
            //
            // A layout often leaves out where its title goes, because the slide master already
            // says so. PowerPoint then takes the place from the master, and so do we:
            //
            master.Placeholders.TryGetValue(MasterTypeOf(placeholder.Type), out var inherited);
            if ((placeholder.Bounds ?? inherited?.Bounds) is not { } bounds)
                continue;

            var role = RoleOf(placeholder.Type, bounds, slideArea);
            if (!role.IsSlot())
                continue;

            //
            // A table takes its text from the table style, which starts out at the default
            // size of the presentation rather than at the size of the body text:
            //
            var fontSize = role switch
            {
                PlaceholderRole.TABLE => master.DefaultFontSize,
                PlaceholderRole.TITLE => placeholder.FontSize ?? inherited?.FontSize ?? master.TitleFontSize,
                _ => placeholder.FontSize ?? inherited?.FontSize ?? master.BodyFontSize,
            };

            slots.Add(new TemplateSlot(role, 0, bounds, placeholder.Type, placeholder.Index, CapacityOf(role, bounds, fontSize, placeholder.Insets)));
        }

        return new TemplateLayout(
            layout.CommonSlideData?.Name?.Value ?? string.Empty,
            partUri,
            layout.Type?.InnerText ?? string.Empty,
            InReadingOrder(slots, slideHeight));
    }

    private static IEnumerable<PlaceholderInfo> PlaceholdersOf(P.CommonSlideData? commonSlideData)
    {
        foreach (var element in commonSlideData?.ShapeTree?.Elements() ?? [])
        {
            P.PlaceholderShape? placeholder;
            SlotBounds? bounds;
            P.TextBody? textBody = null;
            switch (element)
            {
                case P.Shape shape:
                    placeholder = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    bounds = BoundsOf(shape.ShapeProperties?.Transform2D?.Offset, shape.ShapeProperties?.Transform2D?.Extents);
                    textBody = shape.TextBody;
                    break;

                case P.Picture picture:
                    placeholder = picture.NonVisualPictureProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    bounds = BoundsOf(picture.ShapeProperties?.Transform2D?.Offset, picture.ShapeProperties?.Transform2D?.Extents);
                    break;

                case P.GraphicFrame frame:
                    placeholder = frame.NonVisualGraphicFrameProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    bounds = BoundsOf(frame.Transform?.Offset, frame.Transform?.Extents);
                    break;

                default:
                    continue;
            }

            if (placeholder is null)
                continue;

            yield return new PlaceholderInfo(
                // A placeholder without a type is an object placeholder, according to the standard:
                placeholder.Type?.InnerText ?? "obj",
                placeholder.Index?.Value,
                bounds,
                FontSizeOf(textBody?.ListStyle?.Level1ParagraphProperties),
                InsetsOf(textBody?.BodyProperties),
                element);
        }
    }

    /// <summary>
    /// The type of the master placeholder a layout placeholder of the given type takes its defaults from.
    /// </summary>
    private static string MasterTypeOf(string placeholderType) => placeholderType switch
    {
        "title" or "ctrTitle" => "title",
        "dt" => "dt",
        "ftr" => "ftr",
        "sldNum" => "sldNum",

        _ => "body",
    };

    private static PlaceholderRole RoleOf(string placeholderType, SlotBounds bounds, double slideArea) => placeholderType switch
    {
        "title" or "ctrTitle" => PlaceholderRole.TITLE,
        "subTitle" => PlaceholderRole.SUBTITLE,
        "pic" => PlaceholderRole.PICTURE,
        "tbl" => PlaceholderRole.TABLE,
        "ftr" => PlaceholderRole.FOOTER,
        "sldNum" => PlaceholderRole.SLIDE_NUMBER,
        "dt" => PlaceholderRole.DATE,
        "body" or "obj" => bounds.Area < slideArea * LABEL_AREA_SHARE ? PlaceholderRole.LABEL : PlaceholderRole.TEXT,

        _ => PlaceholderRole.UNSUPPORTED,
    };

    private static TextCapacity? CapacityOf(PlaceholderRole role, SlotBounds bounds, int fontSize, TextInsets insets)
    {
        if (role is PlaceholderRole.PICTURE)
            return null;

        var fontHeight = fontSize / 100.0 * EMU_PER_POINT;
        var lineHeight = fontHeight * LINE_HEIGHT_FACTOR;
        var width = (double)bounds.Width;
        var height = (double)bounds.Height;
        if (role is PlaceholderRole.TABLE)
        {
            // A table row keeps the default cell margins above and below its text:
            lineHeight += 2 * DEFAULT_VERTICAL_INSET;
        }
        else
        {
            width -= insets.Left + insets.Right;
            height -= insets.Top + insets.Bottom;
        }

        var lines = (int)(height / lineHeight);
        var charactersPerLine = (int)(width / (fontHeight * CHARACTER_WIDTH_FACTOR));
        return new TextCapacity(Math.Max(1, lines), Math.Max(1, charactersPerLine));
    }

    /// <summary>
    /// Brings the slots into the order a reader takes them in, and numbers the slots of each role.
    /// </summary>
    private static List<TemplateSlot> InReadingOrder(IEnumerable<TemplateSlot> slots, long slideHeight)
    {
        var ordered = slots
            .OrderBy(slot => ReadingKeyOf(slot.Bounds, slideHeight))
            .ToList();

        var counters = new Dictionary<PlaceholderRole, int>();
        for (var index = 0; index < ordered.Count; index++)
        {
            var ordinal = counters.GetValueOrDefault(ordered[index].Role) + 1;
            counters[ordered[index].Role] = ordinal;
            ordered[index] = ordered[index] with { Ordinal = ordinal };
        }

        return ordered;
    }

    /// <summary>
    /// Where a reader takes something in: by its row first, then from left to right.
    /// </summary>
    private static (int Row, long X) ReadingKeyOf(SlotBounds bounds, long slideHeight) => ((int)Math.Round(bounds.Y * (double)READING_ROWS / slideHeight), bounds.X);

    private static SlotBounds? BoundsOf(A.Offset? offset, A.Extents? extents)
    {
        if (offset?.X?.Value is not { } x || offset.Y?.Value is not { } y || extents?.Cx?.Value is not { } width || extents.Cy?.Value is not { } height)
            return null;

        return new SlotBounds(x, y, width, height);
    }

    private static int? FontSizeOf(A.Level1ParagraphProperties? properties) => properties?.GetFirstChild<A.DefaultRunProperties>()?.FontSize?.Value;

    private static TextInsets InsetsOf(A.BodyProperties? bodyProperties) => new(
        bodyProperties?.LeftInset?.Value ?? DEFAULT_HORIZONTAL_INSET,
        bodyProperties?.TopInset?.Value ?? DEFAULT_VERTICAL_INSET,
        bodyProperties?.RightInset?.Value ?? DEFAULT_HORIZONTAL_INSET,
        bodyProperties?.BottomInset?.Value ?? DEFAULT_VERTICAL_INSET);

    /// <summary>
    /// A placeholder as the slide, the layout or the slide master states it, before anything is inherited.
    /// </summary>
    /// <param name="Element">The shape, picture or graphic frame which is the placeholder, e.g., to read its text.</param>
    private sealed record PlaceholderInfo(string Type, uint? Index, SlotBounds? Bounds, int? FontSize, TextInsets Insets, OpenXmlElement Element);

    /// <summary>
    /// What the layouts of one slide master inherit from it: its placeholders, keyed by the type
    /// layout placeholders look them up with, and its font sizes.
    /// </summary>
    private sealed record MasterStyles(IReadOnlyDictionary<string, PlaceholderInfo> Placeholders, int TitleFontSize, int BodyFontSize, int DefaultFontSize);

    private readonly record struct TextInsets(long Left, long Top, long Right, long Bottom);
}