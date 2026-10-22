using System.Text;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// One table of a glossary: each column holds one language, each entry names the same term in all of them.
/// </summary>
/// <remarks>
/// The columns are not tied to a translation direction. A table with the columns German and English serves
/// a translation from German to English just as well as the other way round, so nobody has to maintain the
/// same glossary twice.
/// </remarks>
/// <param name="Languages">The names of the columns, as the header row of the file states them.</param>
/// <param name="Entries">The terms; every entry has exactly one cell per column, empty where a term is missing.</param>
public sealed record GlossaryTable(IReadOnlyList<string> Languages, IReadOnlyList<IReadOnlyList<string>> Entries)
{
    /// <summary>
    /// Renders the table as a Markdown table, which the model reads together with the column names.
    /// </summary>
    /// <returns>The Markdown table, or an empty string when the table has no entries.</returns>
    public string ToMarkdownTable()
    {
        if (this.Entries.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        AppendRow(builder, this.Languages);
        builder.Append('|');
        foreach (var _ in this.Languages)
            builder.Append(" --- |");

        builder.AppendLine();
        foreach (var entry in this.Entries)
            AppendRow(builder, entry);

        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> cells)
    {
        builder.Append('|');
        foreach (var cell in cells)
        {
            builder.Append(' ');
            builder.Append(EscapeCell(cell));
            builder.Append(" |");
        }

        builder.AppendLine();
    }

    //
    // A line break or a pipe inside a cell would end the cell, or the whole row, in the middle of a term:
    //
    private static string EscapeCell(string cell) => cell
        .Replace("\r\n", " ")
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Replace("|", "\\|");
}