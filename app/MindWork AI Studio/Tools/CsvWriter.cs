using System.Globalization;

namespace AIStudio.Tools;

/// <summary>
/// Writes rows of character-separated values. Fields are quoted according to RFC 4180 using the
/// separator of the respective file.
/// </summary>
public static class CsvWriter
{
    /// <summary>
    /// The separator a spreadsheet expects from a CSV file written for the given language.
    /// </summary>
    /// <remarks>
    /// Wherever a comma separates the decimals of a number, it cannot separate the columns of a
    /// file as well: German Excel therefore expects a semicolon and puts a comma-separated file
    /// into a single column. This is the same rule Excel itself follows when it writes a CSV, so
    /// we ask the culture rather than keeping a list of languages of our own.
    /// </remarks>
    /// <param name="ietfTag">The IETF tag of the language, for example "de-DE".</param>
    /// <returns>The separator to write with.</returns>
    public static char SeparatorFor(string ietfTag)
    {
        if (string.IsNullOrWhiteSpace(ietfTag))
            return ',';

        try
        {
            var culture = CultureInfo.GetCultureInfo(ietfTag);
            return culture.NumberFormat.NumberDecimalSeparator is "," ? ';' : ',';
        }
        catch (CultureNotFoundException)
        {
            return ',';
        }
    }

    /// <summary>
    /// Joins the given fields into one row.
    /// </summary>
    /// <param name="separator">The separator between two fields.</param>
    /// <param name="fields">The fields of the row.</param>
    /// <returns>The row, without a line ending.</returns>
    public static string ToRow(char separator, params string[] fields) => string.Join(separator, fields.Select(field => ToField(field, separator)));

    /// <summary>
    /// Quotes one field according to RFC 4180.
    /// </summary>
    private static string ToField(string text, char separator)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Quoting the complete field is important for long and multi-line AI
        // answers: neither separators nor line breaks within an answer may
        // create another column or row.
        if (!text.Contains(separator) && !text.Contains('"') && !text.Contains('\n') && !text.Contains('\r'))
            return text;

        return $"""
                "{text.Replace("\"", "\"\"")}"
                """;
    }
}