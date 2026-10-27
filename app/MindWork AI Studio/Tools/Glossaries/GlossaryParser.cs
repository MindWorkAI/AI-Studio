using System.Text;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Reads the tables to import into a glossary from the text the runtime extracted from a spreadsheet or a CSV file.
/// </summary>
/// <remarks>
/// The first row of each table names the columns, and every further row holds one entry. Which column
/// holds the terms and which the values, the user decides while importing.<br/><br/>
///
/// The runtime fences both kinds of files as <c>```csv</c>: a spreadsheet as one block per sheet, which
/// it writes itself as RFC 4180 CSV with commas, and a CSV or TSV file as one block holding the file as
/// it is. Only the latter may use semicolons or tabs, e.g. an export of a German Excel, so whoever reads
/// the file has to say which kind it was; the fence does not tell.
/// </remarks>
public static class GlossaryParser
{
    private const string CSV_FENCE_START = "```csv";
    private const string FENCE_END = "```";

    /// <summary>
    /// Reads all tables which can fill a glossary.
    /// </summary>
    /// <param name="content">The extracted text of the file.</param>
    /// <param name="isDelimitedTextFile">Whether the file is a CSV or TSV file, whose delimiter has to be detected, rather than a spreadsheet.</param>
    /// <returns>The tables with at least two columns and one row, in the order of the file.</returns>
    public static IReadOnlyList<GlossaryImportTable> Parse(string content, bool isDelimitedTextFile = false)
    {
        content = content.TrimStart('﻿');
        var tables = new List<GlossaryImportTable>();
        var blocks = FindSpreadsheetBlocks(content);
        if (blocks is null)
        {
            // A CSV file: German exports of Excel use semicolons, others commas or tabs.
            if (TryCreateTable(ReadRows(content, DetectDelimiter(content)), out var table))
                tables.Add(table);

            return tables;
        }

        //
        // The runtime writes every sheet as RFC 4180 CSV with commas, whatever the locale of the user.
        // A fenced CSV or TSV file keeps the delimiter it was saved with:
        //
        foreach (var block in blocks)
            if (TryCreateTable(ReadRows(block, isDelimitedTextFile ? DetectDelimiter(block) : ','), out var table))
                tables.Add(table);

        return tables;
    }

    /// <summary>
    /// Finds the fenced CSV blocks of an extracted spreadsheet.
    /// </summary>
    /// <returns>The text of each block, or null when the content holds no such block at all.</returns>
    private static List<string>? FindSpreadsheetBlocks(string content)
    {
        List<string>? blocks = null;
        StringBuilder? currentBlock = null;
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (currentBlock is null)
            {
                if (line.Trim() == CSV_FENCE_START)
                    currentBlock = new StringBuilder();

                continue;
            }

            if (line.Trim() == FENCE_END)
            {
                blocks ??= [];
                blocks.Add(currentBlock.ToString());
                currentBlock = null;
                continue;
            }

            currentBlock.Append(line).Append('\n');
        }

        // A block whose end is missing still holds the rows read so far:
        if (currentBlock is not null)
        {
            blocks ??= [];
            blocks.Add(currentBlock.ToString());
        }

        return blocks;
    }

    /// <summary>
    /// Picks the delimiter which separates the most cells of the header row.
    /// </summary>
    /// <remarks>
    /// On a tie, the comma wins, because it is what the standard names.
    /// </remarks>
    private static char DetectDelimiter(string content)
    {
        int commas = 0, semicolons = 0, tabs = 0;
        var insideQuotes = false;
        foreach (var character in content)
        {
            if (character == '"')
                insideQuotes = !insideQuotes;
            else if (!insideQuotes && character is '\n' or '\r')
                break;
            else if (!insideQuotes && character == ',')
                commas++;
            else if (!insideQuotes && character == ';')
                semicolons++;
            else if (!insideQuotes && character == '\t')
                tabs++;
        }

        if (semicolons > commas && semicolons >= tabs)
            return ';';

        if (tabs > commas && tabs > semicolons)
            return '\t';

        return ',';
    }

    /// <summary>
    /// Splits CSV text into rows and cells as RFC 4180 describes it.
    /// </summary>
    /// <remarks>
    /// A quoted cell may hold the delimiter, line breaks, and doubled quotes. Quotes inside an unquoted
    /// cell are kept as they are, since that is what a reader of the file would expect to see.
    /// </remarks>
    private static List<List<string>> ReadRows(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var insideQuotes = false;
        var cellStarted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (insideQuotes)
            {
                if (character != '"')
                    cell.Append(character);
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                    insideQuotes = false;

                continue;
            }

            if (character == '"' && !cellStarted)
            {
                insideQuotes = true;
                cellStarted = true;
            }
            else if (character == delimiter)
            {
                row.Add(cell.ToString());
                cell.Clear();
                cellStarted = false;
            }
            else if (character is '\n' or '\r')
            {
                if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                row.Add(cell.ToString());
                rows.Add(row);
                row = [];
                cell.Clear();
                cellStarted = false;
            }
            else
            {
                cell.Append(character);
                cellStarted = true;
            }
        }

        if (cellStarted || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Turns the rows of one block into a table: the first row with content names the columns.
    /// </summary>
    /// <remarks>
    /// Spreadsheets often carry empty rows and columns around the data, e.g. because a cell was formatted
    /// once. They are dropped, so the user does not have to tell them apart from the columns with data.
    /// </remarks>
    private static bool TryCreateTable(List<List<string>> rows, out GlossaryImportTable table)
    {
        table = null!;
        var cleanRows = rows
            .Select(row => row.Select(cell => cell.Trim()).ToList())
            .Where(row => row.Any(cell => cell.Length > 0))
            .ToList();

        if (cleanRows.Count < 2)
            return false;

        var width = cleanRows.Max(row => row.Count);
        var usedColumns = Enumerable.Range(0, width)
            .Where(column => cleanRows.Any(row => column < row.Count && row[column].Length > 0))
            .ToList();

        if (usedColumns.Count < 2)
            return false;

        var header = cleanRows[0];
        var columns = usedColumns
            .Select((column, index) => column < header.Count && header[column].Length > 0 ? header[column] : $"Column {index + 1}")
            .ToList();

        var dataRows = cleanRows
            .Skip(1)
            .Select(row => (IReadOnlyList<string>)usedColumns.Select(column => column < row.Count ? row[column] : string.Empty).ToList())
            .ToList();

        table = new GlossaryImportTable(columns, dataRows);
        return true;
    }
}