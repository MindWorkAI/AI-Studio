using System.Text;

namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// Reads and writes the CSV files of the batch processing assistant. Fields
/// are quoted according to RFC 4180 using the separator selected for the
/// respective file.
/// </summary>
public static class BatchProcessingCsv
{
    public static string ToCsvRow(char separator, params string[] fields) => string.Join(separator, fields.Select(field => ToCsvField(field, separator)));

    /// <summary>
    /// Quotes one CSV field according to RFC 4180.
    /// </summary>
    private static string ToCsvField(string text, char separator)
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

    /// <summary>
    /// Parses a CSV text which was written by <see cref="ToCsvRow"/>.
    /// </summary>
    /// <remarks>
    /// We parse the file ourselves instead of splitting lines, because quoted
    /// fields may contain the separator and line breaks.
    /// </remarks>
    private static List<List<string>> Parse(string content, char separator)
    {
        var rows = new List<List<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var isQuoted = false;
        var hasContent = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (isQuoted)
            {
                if (character is not '"')
                {
                    field.Append(character);
                    continue;
                }

                // A doubled quote is an escaped quote, everything else ends the quoted field:
                if (index + 1 < content.Length && content[index + 1] is '"')
                {
                    field.Append('"');
                    index++;
                    continue;
                }

                isQuoted = false;
                continue;
            }

            switch (character)
            {
                case '"':
                    isQuoted = true;
                    hasContent = true;
                    break;

                case var _ when character == separator:
                    hasContent = true;
                    EndField();
                    break;

                case '\r':
                    break;

                case '\n':
                    EndRow();
                    break;

                default:
                    hasContent = true;
                    field.Append(character);
                    break;
            }
        }

        if (hasContent || field.Length > 0)
            EndRow();

        return rows;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            if (hasContent)
                rows.Add([..fields]);

            fields.Clear();
            hasContent = false;
        }
    }

    /// <summary>
    /// Detects the separator from the first CSV record and parses the complete
    /// content with it. Preferred separators are used as fallbacks for files
    /// whose first record does not reveal a valid separator.
    /// </summary>
    public static List<List<string>> ParseWithDetectedSeparator(string content, int expectedNumFields, params char[] preferredSeparators)
    {
        var firstRecord = ReadFirstRecord(content);
        var candidates = new List<char>();
        var isQuoted = false;
        for (var index = 0; index < firstRecord.Length; index++)
        {
            var character = firstRecord[index];
            if (character is '"')
            {
                if (isQuoted && index + 1 < firstRecord.Length && firstRecord[index + 1] is '"')
                {
                    index++;
                    continue;
                }

                isQuoted = !isQuoted;
                continue;
            }

            if (!isQuoted
                && character is not '\r' and not '\n'
                && (char.IsPunctuation(character) || char.IsSymbol(character) || character is '\t')
                && !candidates.Contains(character))
                candidates.Add(character);
        }

        foreach (var separator in preferredSeparators)
        {
            if (!candidates.Contains(separator))
                candidates.Add(separator);
        }

        foreach (var separator in candidates)
        {
            var header = Parse(firstRecord, separator);
            if (header.Count is 1 && header[0].Count == expectedNumFields)
                return Parse(content, separator);
        }

        throw new InvalidDataException("Was not able to detect the CSV separator.");
    }

    private static string ReadFirstRecord(string content)
    {
        var isQuoted = false;
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] is '"')
            {
                if (isQuoted && index + 1 < content.Length && content[index + 1] is '"')
                {
                    index++;
                    continue;
                }

                isQuoted = !isQuoted;
            }
            else if (content[index] is '\n' && !isQuoted)
                return content[..(index + 1)];
        }

        return content;
    }
}