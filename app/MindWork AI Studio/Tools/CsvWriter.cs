namespace AIStudio.Tools;

/// <summary>
/// Writes rows of character-separated values. Fields are quoted according to RFC 4180 using the
/// separator of the respective file.
/// </summary>
public static class CsvWriter
{
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