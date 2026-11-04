using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// The page of a section and the area of it in which the text runs, in twentieths of a point.
/// </summary>
/// <param name="PageWidth">The width of the page.</param>
/// <param name="PageHeight">The height of the page.</param>
/// <param name="Top">The space between the top edge of the page and the text.</param>
/// <param name="Right">The space between the text and the right edge of the page.</param>
/// <param name="Bottom">The space between the text and the bottom edge of the page.</param>
/// <param name="Left">The space between the left edge of the page and the text.</param>
/// <param name="Gutter">The extra space for binding.</param>
/// <param name="Columns">The number of columns of the text.</param>
/// <param name="ColumnSpace">The space between two columns.</param>
internal readonly record struct WordTextArea(int PageWidth, int PageHeight, int Top, int Right, int Bottom, int Left, int Gutter, int Columns, int ColumnSpace)
{
    /// <summary>
    /// How many EMU, the unit of positions and sizes of drawings, make a twentieth of a point.
    /// </summary>
    public const long EMU_PER_TWIP = 635;

    /// <summary>
    /// Reads the page and the text area of a section.
    /// </summary>
    /// <remarks>
    /// A negative top or bottom margin keeps its size even when the header or footer grows, which
    /// makes no difference here. What the section does not state takes the values Word uses then:
    /// a page of US Letter with margins of one inch.
    /// </remarks>
    /// <param name="section">The properties of the section.</param>
    /// <returns>The page and its text area.</returns>
    public static WordTextArea Of(W.SectionProperties section)
    {
        var size = section.GetFirstChild<W.PageSize>();
        var margin = section.GetFirstChild<W.PageMargin>();
        var columns = section.GetFirstChild<W.Columns>();
        return new(
            (int)(size?.Width?.Value ?? 12_240U),
            (int)(size?.Height?.Value ?? 15_840U),
            Math.Abs(margin?.Top?.Value ?? 1440),
            (int)(margin?.Right?.Value ?? 1440U),
            Math.Abs(margin?.Bottom?.Value ?? 1440),
            (int)(margin?.Left?.Value ?? 1440U),
            (int)(margin?.Gutter?.Value ?? 0U),
            Math.Max(1, (int)(columns?.ColumnCount?.Value ?? 1)),
            int.TryParse(columns?.Space?.Value, out var space) ? space : 720);
    }

    /// <summary>
    /// The width of a column of the text.
    /// </summary>
    public int ColumnWidth => Math.Max(1, (this.PageWidth - this.Left - this.Right - this.Gutter - this.ColumnSpace * (this.Columns - 1)) / this.Columns);

    /// <summary>
    /// The height of a column of the text.
    /// </summary>
    public int ColumnHeight => Math.Max(1, this.PageHeight - this.Top - this.Bottom);

    /// <summary>
    /// Determines whether the text runs in the same columns as in another section, apart from where it starts and ends.
    /// </summary>
    public bool HasSameColumnsAs(WordTextArea other) => this with { PageHeight = other.PageHeight, Top = other.Top, Bottom = other.Bottom } == other;
}