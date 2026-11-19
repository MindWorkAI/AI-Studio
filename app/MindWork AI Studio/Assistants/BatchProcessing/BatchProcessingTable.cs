using System.Text;

namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// One sheet of a table which a batch run goes through.
/// </summary>
/// <param name="Name">The name of the sheet, or empty for a CSV file, which has no sheets.</param>
/// <param name="Rows">The cells of the sheet, row by row.</param>
public sealed record BatchProcessingSheet(string Name, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// One row or column of a table, which a batch run hands to the model as one document.
/// </summary>
/// <param name="Index">The index of the row or column within the sheet, counted from zero.</param>
/// <param name="Text">The values of the row or column, each after its heading.</param>
public sealed record BatchProcessingTableEntry(int Index, string Text);

/// <summary>
/// Reads a table which the user picked as the input of a batch run, and splits it into its rows or columns.
/// </summary>
/// <remarks>
/// The table is read by the runtime, like every document of a batch run, so its text passes the filter
/// for prompt injections. The runtime hands a workbook over as one CSV block per sheet, under a heading
/// with the name of the sheet, and a CSV file as it is, inside a code fence. This class reads both back
/// into cells.
/// </remarks>
public static class BatchProcessingTable
{
    private const string SHEET_HEADING = "# ";
    private const string CSV_FENCE = "```csv";
    private const string FENCE = "```";

    /// <summary>
    /// Whether a file is a CSV or TSV file rather than a workbook with sheets.
    /// </summary>
    public static bool IsCsvFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) || extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the sheets of a table from what the runtime extracted from it.
    /// </summary>
    /// <param name="extractedContent">The text the runtime extracted from the table.</param>
    /// <param name="isCsvFile">Whether the table is a CSV file, cf. IsCsvFile.</param>
    /// <returns>The sheets of a workbook, or the one sheet without a name of a CSV file.</returns>
    public static IReadOnlyList<BatchProcessingSheet> ReadSheets(string extractedContent, bool isCsvFile)
    {
        var content = extractedContent.ReplaceLineEndings("\n");
        return isCsvFile ? [ReadCsvSheet(content)] : ReadWorkbookSheets(content);
    }

    private static BatchProcessingSheet ReadCsvSheet(string content)
    {
        var body = content.Trim('\n');
        if (body.StartsWith(CSV_FENCE, StringComparison.Ordinal))
        {
            var firstLineEnd = body.IndexOf('\n');
            body = firstLineEnd < 0 ? string.Empty : body[(firstLineEnd + 1)..];
        }

        var trimmed = body.TrimEnd('\n');
        if (trimmed.EndsWith(FENCE, StringComparison.Ordinal) && (trimmed.Length == FENCE.Length || trimmed[^(FENCE.Length + 1)] is '\n'))
            body = trimmed[..^FENCE.Length];

        return new BatchProcessingSheet(string.Empty, BatchProcessingCsv.ParseWithLikelySeparator(body));
    }

    /// <remarks>
    /// The runtime writes a sheet as a heading with its name, a line opening a CSV fence, one line per
    /// row with its cells separated by commas and quoted where needed, and a closing fence. A quoted cell
    /// may span several lines, so the closing fence is only looked for where a row starts.
    /// </remarks>
    private static IReadOnlyList<BatchProcessingSheet> ReadWorkbookSheets(string content)
    {
        var sheets = new List<BatchProcessingSheet>();
        string? sheetName = null;
        var position = 0;
        while (position < content.Length)
        {
            var line = ReadLine(content, ref position);
            if (line.StartsWith(SHEET_HEADING, StringComparison.Ordinal))
            {
                sheetName = line[SHEET_HEADING.Length..];
                continue;
            }

            if (line != CSV_FENCE || sheetName is null)
                continue;

            var start = position;
            var end = FindEndOfSheet(content, ref position);
            sheets.Add(new BatchProcessingSheet(sheetName, BatchProcessingCsv.Parse(content[start..end], ',')));
            sheetName = null;
        }

        return sheets;
    }

    private static string ReadLine(string content, ref int position)
    {
        var lineEnd = content.IndexOf('\n', position);
        if (lineEnd < 0)
            lineEnd = content.Length;

        var line = content[position..lineEnd];
        position = Math.Min(lineEnd + 1, content.Length);
        return line;
    }

    /// <summary>
    /// Finds the closing fence of a sheet, starting at its first row.
    /// </summary>
    /// <param name="content">The extracted text.</param>
    /// <param name="position">The start of the first row; afterward, the position behind the closing fence.</param>
    /// <returns>The end of the rows of the sheet.</returns>
    private static int FindEndOfSheet(string content, ref int position)
    {
        while (position < content.Length)
        {
            var rowStart = position;
            var lineEnd = content.IndexOf('\n', rowStart);
            var line = lineEnd < 0 ? content[rowStart..] : content[rowStart..lineEnd];
            if (line == FENCE)
            {
                position = lineEnd < 0 ? content.Length : lineEnd + 1;
                return rowStart;
            }

            //
            // Skip the row, including the line breaks within its quoted cells:
            //
            var isQuoted = false;
            while (position < content.Length)
            {
                var character = content[position++];
                if (character is '"')
                    isQuoted = !isQuoted;
                else if (character is '\n' && !isQuoted)
                    break;
            }
        }

        return content.Length;
    }

    /// <summary>
    /// Splits a sheet into the rows or columns which a batch run hands to the model one by one.
    /// </summary>
    /// <remarks>
    /// Going through the rows, the first row holds the headings of the columns, and each further row
    /// becomes one entry. Going through the columns, the first column holds the headings of the rows,
    /// and each further column becomes one entry. Each value of an entry goes after its heading, so the
    /// model knows what it means. Empty values are left out, and a row or column without any value
    /// is no entry at all.
    /// </remarks>
    /// <param name="rows">The cells of the sheet, row by row.</param>
    /// <param name="direction">Whether each row or each column is one entry.</param>
    /// <returns>The entries, in the order of the sheet.</returns>
    public static IReadOnlyList<BatchProcessingTableEntry> SplitIntoEntries(IReadOnlyList<IReadOnlyList<string>> rows, BatchProcessingTableDirection direction)
    {
        var entries = new List<BatchProcessingTableEntry>();
        if (direction is BatchProcessingTableDirection.ROWS)
        {
            var headings = rows.Count > 0 ? rows[0] : [];
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var text = new StringBuilder();
                for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
                    AppendValue(text, CellOf(headings, columnIndex), $"Column {BatchProcessingExcel.ColumnName(columnIndex)}", rows[rowIndex][columnIndex]);

                if (text.Length > 0)
                    entries.Add(new BatchProcessingTableEntry(rowIndex, text.ToString().TrimEnd('\n')));
            }

            return entries;
        }

        var numColumns = rows.Count is 0 ? 0 : rows.Max(row => row.Count);
        for (var columnIndex = 1; columnIndex < numColumns; columnIndex++)
        {
            var text = new StringBuilder();
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                AppendValue(text, CellOf(rows[rowIndex], 0), $"Row {rowIndex + 1}", CellOf(rows[rowIndex], columnIndex));

            if (text.Length > 0)
                entries.Add(new BatchProcessingTableEntry(columnIndex, text.ToString().TrimEnd('\n')));
        }

        return entries;
    }

    private static string CellOf(IReadOnlyList<string> row, int index) => index < row.Count ? row[index] : string.Empty;

    private static void AppendValue(StringBuilder text, string heading, string fallbackHeading, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var name = string.IsNullOrWhiteSpace(heading) ? fallbackHeading : heading.Trim();
        text.AppendLineLF($"{name}: {value.Trim()}");
    }
}