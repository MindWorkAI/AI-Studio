using DocumentFormat.OpenXml.Packaging;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Finds the styles of a Word template which the converted content uses, and adds those it lacks.
/// </summary>
/// <remarks>
/// A style is found by the name Word gives its built-in styles, e.g., "heading 1", rather than by
/// its ID: the ID depends on the language of the Word which saved the template, so that a German
/// Word stores the first heading as "berschrift1", whereas the name stays the same in every
/// language. The ID serves only as a second guess, for files of programs which write no names.
///
/// When the template lacks a style, a plain definition of it is added, much as Pandoc does with a
/// reference document. Apart from these, the content carries no formatting of its own, so that the
/// design of the template applies to all of it.
/// </remarks>
internal sealed class WordStyles
{
    /// <summary>
    /// The font sizes of the headings which a template lacks, in half points: 16 pt down to 11 pt.
    /// </summary>
    private static readonly string[] HEADING_FONT_SIZES = ["32", "28", "26", "24", "22", "22"];

    private readonly W.Styles styles;
    private readonly string? defaultParagraphStyleId;
    private readonly string? defaultTableStyleId;
    private readonly Dictionary<string, string> idsByName = new(StringComparer.OrdinalIgnoreCase);

    private WordStyles(W.Styles styles, string? defaultTableStyleId)
    {
        this.styles = styles;
        this.defaultTableStyleId = defaultTableStyleId;
        var defaultParagraphStyle = styles.Elements<W.Style>().FirstOrDefault(style => IsOfType(style, "paragraph") && style.Default?.Value is true);
        this.defaultParagraphStyleId = defaultParagraphStyle?.StyleId?.Value;
    }

    /// <summary>
    /// Reads the styles of a document, and adds a part for them when it has none.
    /// </summary>
    /// <param name="mainPart">The main part of the document.</param>
    /// <returns>The styles.</returns>
    public static WordStyles Of(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.StyleDefinitionsPart ?? mainPart.AddNewPart<StyleDefinitionsPart>();
        var defaultTableStyleId = mainPart.DocumentSettingsPart?.Settings?.GetFirstChild<W.DefaultTableStyle>()?.Val?.Value;
        return new WordStyles(stylesPart.Styles ??= new W.Styles(), defaultTableStyleId);
    }

    /// <summary>
    /// Returns the ID of the paragraph style of a heading.
    /// </summary>
    /// <param name="level">The level of the heading, from 1 to 6.</param>
    /// <returns>The ID of the style.</returns>
    public string HeadingOf(int level)
    {
        level = Math.Clamp(level, 1, HEADING_FONT_SIZES.Length);
        return this.IdOf("paragraph", $"heading {level}", () => new W.Style
        {
            StyleName = new W.StyleName { Val = $"heading {level}" },
            BasedOn = this.defaultParagraphStyleId is { } basedOn ? new W.BasedOn { Val = basedOn } : null,
            NextParagraphStyle = this.defaultParagraphStyleId is { } next ? new W.NextParagraphStyle { Val = next } : null,
            UIPriority = new W.UIPriority { Val = 9 },
            PrimaryStyle = new W.PrimaryStyle(),
            StyleParagraphProperties = new W.StyleParagraphProperties
            {
                KeepNext = new W.KeepNext(),
                KeepLines = new W.KeepLines(),
                SpacingBetweenLines = new W.SpacingBetweenLines { Before = "240", After = "60" },
                OutlineLevel = new W.OutlineLevel { Val = level - 1 },
            },
            StyleRunProperties = new W.StyleRunProperties
            {
                Bold = new W.Bold(),
                FontSize = new W.FontSize { Val = HEADING_FONT_SIZES[level - 1] },
                FontSizeComplexScript = new W.FontSizeComplexScript { Val = HEADING_FONT_SIZES[level - 1] },
            },
        });
    }

    /// <summary>
    /// The ID of the character style of a link.
    /// </summary>
    public string Hyperlink => this.IdOf("character", "Hyperlink", () => new W.Style
    {
        StyleName = new W.StyleName { Val = "Hyperlink" },
        UIPriority = new W.UIPriority { Val = 99 },
        UnhideWhenUsed = new W.UnhideWhenUsed(),
        StyleRunProperties = new W.StyleRunProperties
        {
            Color = new W.Color { Val = "0563C1", ThemeColor = W.ThemeColorValues.Hyperlink },
            Underline = new W.Underline { Val = W.UnderlineValues.Single },
        },
    });

    /// <summary>
    /// The ID of the character style of code within a line of text.
    /// </summary>
    public string InlineCode => this.IdOf("character", "HTML Code", () => new W.Style
    {
        StyleName = new W.StyleName { Val = "HTML Code" },
        UIPriority = new W.UIPriority { Val = 99 },
        UnhideWhenUsed = new W.UnhideWhenUsed(),
        StyleRunProperties = CodeRunProperties(),
    });

    /// <summary>
    /// The ID of the paragraph style of a block of code.
    /// </summary>
    public string CodeBlock => this.IdOf("paragraph", "HTML Preformatted", () => new W.Style
    {
        StyleName = new W.StyleName { Val = "HTML Preformatted" },
        BasedOn = this.defaultParagraphStyleId is { } basedOn ? new W.BasedOn { Val = basedOn } : null,
        UIPriority = new W.UIPriority { Val = 99 },
        UnhideWhenUsed = new W.UnhideWhenUsed(),
        StyleParagraphProperties = new W.StyleParagraphProperties
        {
            SpacingBetweenLines = new W.SpacingBetweenLines { After = "120" },
        },
        StyleRunProperties = CodeRunProperties(),
    });

    /// <summary>
    /// The ID of the paragraph style of the items of a list.
    /// </summary>
    public string ListParagraph => this.IdOf("paragraph", "List Paragraph", () => new W.Style
    {
        StyleName = new W.StyleName { Val = "List Paragraph" },
        BasedOn = this.defaultParagraphStyleId is { } basedOn ? new W.BasedOn { Val = basedOn } : null,
        UIPriority = new W.UIPriority { Val = 34 },
        PrimaryStyle = new W.PrimaryStyle(),
        StyleParagraphProperties = new W.StyleParagraphProperties
        {
            ContextualSpacing = new W.ContextualSpacing(),
            Indentation = new W.Indentation { Left = WordNumbering.IndentationOf(0).ToString() },
        },
    });

    /// <summary>
    /// The ID of the paragraph style of a quotation.
    /// </summary>
    public string Quote => this.IdOf("paragraph", "Quote", () => new W.Style
    {
        StyleName = new W.StyleName { Val = "Quote" },
        BasedOn = this.defaultParagraphStyleId is { } basedOn ? new W.BasedOn { Val = basedOn } : null,
        NextParagraphStyle = this.defaultParagraphStyleId is { } next ? new W.NextParagraphStyle { Val = next } : null,
        UIPriority = new W.UIPriority { Val = 29 },
        PrimaryStyle = new W.PrimaryStyle(),
        StyleParagraphProperties = new W.StyleParagraphProperties
        {
            Indentation = new W.Indentation { Left = "864", Right = "864" },
        },
        StyleRunProperties = new W.StyleRunProperties { Italic = new W.Italic() },
    });

    /// <summary>
    /// The ID of the table style: the one the template names for new tables, otherwise the plain
    /// grid which Word offers first.
    /// </summary>
    public string Table => this.defaultTableStyleId is { } defaultId && this.styles.Elements<W.Style>().Any(style => IsOfType(style, "table") && style.StyleId?.Value == defaultId)
        ? defaultId
        : this.IdOf("table", "Table Grid", () => new W.Style
        {
            StyleName = new W.StyleName { Val = "Table Grid" },
            UIPriority = new W.UIPriority { Val = 39 },
            StyleParagraphProperties = new W.StyleParagraphProperties
            {
                SpacingBetweenLines = new W.SpacingBetweenLines { After = "0" },
            },
            StyleTableProperties = new W.StyleTableProperties
            {
                TableBorders = new W.TableBorders
                {
                    TopBorder = new W.TopBorder { Val = W.BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                    LeftBorder = new W.LeftBorder { Val = W.BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                    BottomBorder = new W.BottomBorder { Val = W.BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                    RightBorder = new W.RightBorder { Val = W.BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                    InsideHorizontalBorder = new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                    InsideVerticalBorder = new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                },
            },
        });

    /// <summary>
    /// The look of code which the template lacks a style for: 10 pt in a font with letters of one width.
    /// </summary>
    private static W.StyleRunProperties CodeRunProperties() => new()
    {
        RunFonts = new W.RunFonts { Ascii = "Courier New", HighAnsi = "Courier New", ComplexScript = "Courier New" },
        FontSize = new W.FontSize { Val = "20" },
        FontSizeComplexScript = new W.FontSizeComplexScript { Val = "20" },
    };

    /// <summary>
    /// Finds a style by its name, and adds it when the template lacks it.
    /// </summary>
    /// <param name="type">The type of the style as the file states it: "paragraph", "character", or "table".</param>
    /// <param name="name">The name of the built-in style, as Word stores it.</param>
    /// <param name="createStyle">Creates a plain definition of the style, without type and ID.</param>
    /// <returns>The ID of the style.</returns>
    private string IdOf(string type, string name, Func<W.Style> createStyle)
    {
        var key = $"{type}:{name}";
        if (this.idsByName.TryGetValue(key, out var id))
            return id;

        var candidates = this.styles.Elements<W.Style>().Where(candidate => IsOfType(candidate, type) && candidate.StyleId?.Value is not null).ToList();
        var guessedId = IdFrom(name);
        var found = candidates.FirstOrDefault(candidate => string.Equals(candidate.StyleName?.Val?.Value, name, StringComparison.OrdinalIgnoreCase))
                    ?? candidates.FirstOrDefault(candidate => string.Equals(candidate.StyleId!.Value, guessedId, StringComparison.OrdinalIgnoreCase));

        if (found is null)
        {
            found = createStyle();
            found.Type = type switch
            {
                "character" => W.StyleValues.Character,
                "table" => W.StyleValues.Table,

                _ => W.StyleValues.Paragraph,
            };
            found.StyleId = this.UnusedId(guessedId);
            this.styles.Append(found);
        }

        id = found.StyleId!.Value!;
        this.idsByName[key] = id;
        return id;
    }

    /// <summary>
    /// Returns an ID no style of the document uses yet.
    /// </summary>
    /// <param name="wantedId">The ID the style would get in an English Word.</param>
    /// <returns>The wanted ID, or the wanted ID with a number when another style uses it already.</returns>
    private string UnusedId(string wantedId)
    {
        var usedIds = this.styles.Elements<W.Style>()
            .Select(style => style.StyleId?.Value)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var id = wantedId;
        for (var number = 2; usedIds.Contains(id); number++)
            id = $"{wantedId}{number}";

        return id;
    }

    /// <summary>
    /// Returns the ID an English Word gives a built-in style: its name without spaces, starting
    /// with a capital letter, e.g., "Heading1" for "heading 1".
    /// </summary>
    private static string IdFrom(string name)
    {
        var id = name.Replace(" ", string.Empty);
        return id.Length is 0 ? id : $"{char.ToUpperInvariant(id[0])}{id[1..]}";
    }

    /// <summary>
    /// Whether a style is of the given type. A style which states none is a paragraph style.
    /// </summary>
    private static bool IsOfType(W.Style style, string type) => (style.Type?.InnerText ?? "paragraph") == type;
}