using System.Text;
using System.Xml;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// Writes and reads the results table of a batch run as a Microsoft Excel workbook.
/// </summary>
/// <remarks>
/// The workbook has one worksheet, whose rows are the rows of the table. Every cell holds text,
/// so that Excel never turns an answer such as 1/2 or 007 into a date or a number.
/// </remarks>
internal static class BatchProcessingExcel
{
    /// <summary>
    /// The most characters an Excel cell holds.
    /// </summary>
    public const int MAX_CELL_LENGTH = 32_767;

    /// <summary>
    /// The name of the worksheet. Excel allows at most 31 characters and no []:*?/\ in it.
    /// </summary>
    private const string SHEET_NAME = "Results";

    /// <summary>
    /// Marks an answer which was too long for one cell.
    /// </summary>
    private const string TRUNCATION_MARK = " [...]";

    /// <summary>
    /// Writes the rows as a workbook into the stream.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="rows">The rows, the header first.</param>
    public static void Write(Stream stream, IEnumerable<IReadOnlyList<string>> rows)
    {
        using var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();

        var sheetData = new SheetData();
        uint rowIndex = 1;
        foreach (var values in rows)
        {
            var row = new Row { RowIndex = rowIndex };
            for (var columnIndex = 0; columnIndex < values.Count; columnIndex++)
            {
                row.Append(new Cell
                {
                    CellReference = $"{ColumnName(columnIndex)}{rowIndex}",
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(ToCellText(values[columnIndex])) { Space = SpaceProcessingModeValues.Preserve }),
                });
            }

            sheetData.Append(row);
            rowIndex++;
        }

        worksheetPart.Worksheet = new Worksheet(sheetData);
        workbookPart.Workbook.AppendChild(new Sheets(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = SHEET_NAME }));
    }

    /// <summary>
    /// Reads the rows of the first worksheet of a workbook.
    /// </summary>
    /// <remarks>
    /// Reads what this class writes as well as what Excel makes of it when the user saves the
    /// workbook: Excel moves the texts into a table of shared strings and leaves out empty cells.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The rows, the header first.</returns>
    public static IReadOnlyList<IReadOnlyList<string>> Read(Stream stream)
    {
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart;
        var sheet = workbookPart?.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault();
        if (workbookPart is null || sheet?.Id?.Value is not { } sheetId || workbookPart.GetPartById(sheetId) is not WorksheetPart worksheetPart)
            return [];

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ToList() ?? [];
        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in worksheetPart.Worksheet?.Descendants<Row>() ?? [])
        {
            var values = new List<string>();
            foreach (var cell in row.Elements<Cell>())
            {
                var columnIndex = ColumnIndex(cell.CellReference?.Value) ?? values.Count;
                while (values.Count < columnIndex)
                    values.Add(string.Empty);

                values.Add(CellText(cell, sharedStrings));
            }

            rows.Add(values);
        }

        return rows;
    }

    /// <summary>
    /// Makes a text fit into a cell: without the characters XML cannot hold, and short enough for Excel.
    /// </summary>
    internal static string ToCellText(string text)
    {
        var cleaned = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (XmlConvert.IsXmlChar(text[index]))
                cleaned.Append(text[index]);
            else if (index + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[index + 1], text[index]))
            {
                cleaned.Append(text, index, 2);
                index++;
            }
        }

        if (cleaned.Length <= MAX_CELL_LENGTH)
            return cleaned.ToString();

        //
        // Never cut between the two halves of a surrogate pair, which no XML writer takes:
        //
        var end = MAX_CELL_LENGTH - TRUNCATION_MARK.Length;
        if (char.IsHighSurrogate(cleaned[end - 1]))
            end--;

        return string.Concat(cleaned.ToString(0, end), TRUNCATION_MARK);
    }

    private static string CellText(Cell cell, List<SharedStringItem> sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.InnerText ?? string.Empty;

        var value = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count)
            return sharedStrings[index].InnerText;

        return value;
    }

    /// <summary>
    /// The name of a column, e.g., A for the first and AA for the 27th.
    /// </summary>
    private static string ColumnName(int columnIndex)
    {
        var name = string.Empty;
        for (var number = columnIndex + 1; number > 0; number = (number - 1) / 26)
            name = (char)('A' + (number - 1) % 26) + name;

        return name;
    }

    /// <summary>
    /// The index of the column a cell reference such as B7 names, or null when it names none.
    /// </summary>
    private static int? ColumnIndex(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
            return null;

        var number = 0;
        foreach (var character in cellReference.TakeWhile(char.IsAsciiLetter))
            number = number * 26 + (char.ToUpperInvariant(character) - 'A' + 1);

        return number is 0 ? null : number - 1;
    }
}