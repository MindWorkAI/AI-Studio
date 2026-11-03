namespace AIStudio.Tools;

/// <summary>
/// Where a piece of an extracted document is, as the runtime stated it.
/// </summary>
/// <remarks>
/// Which values a piece has depends on its document: a page for PDF and Word files, a sheet and a
/// row for spreadsheets, a line for text, CSV and source code. Slides are left out while the
/// runtime may deliver the slides of a deck out of order: a slide number would point at the wrong
/// slide.<br/><br/>
/// What the app writes around the content, such as the heading of a sheet or the fence around a
/// table, has no row or line of its own: the runtime numbers it 0, and it is stated as none here.
/// </remarks>
/// <param name="PageNumber">The page, counted from one, or null.</param>
/// <param name="SheetName">The sheet, or null.</param>
/// <param name="RowNumber">The row of the sheet, counted from one as a spreadsheet program counts it, or null.</param>
/// <param name="LineNumber">The line, counted from one, or null.</param>
public readonly record struct SourceLocation(int? PageNumber = null, string? SheetName = null, int? RowNumber = null, int? LineNumber = null)
{
    /// <summary>
    /// A piece on a page.
    /// </summary>
    /// <param name="pageNumber">The page, or null when the runtime stated none.</param>
    public static SourceLocation OnPage(int? pageNumber) => new(PageNumber: pageNumber is > 0 ? pageNumber : null);

    /// <summary>
    /// A row of a sheet.
    /// </summary>
    /// <param name="sheetName">The sheet.</param>
    /// <param name="rowNumber">The row, or 0 for what stands before the first row.</param>
    public static SourceLocation InSheet(string? sheetName, int? rowNumber) => new(SheetName: string.IsNullOrEmpty(sheetName) ? null : sheetName, RowNumber: rowNumber is > 0 ? rowNumber : null);

    /// <summary>
    /// A line of a text.
    /// </summary>
    /// <param name="lineNumber">The line, or 0 for what stands before the first line.</param>
    public static SourceLocation OnLine(int? lineNumber) => new(LineNumber: lineNumber is > 0 ? lineNumber : null);
}