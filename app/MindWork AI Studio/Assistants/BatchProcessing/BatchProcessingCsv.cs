using System.Text;

namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// Reads and writes the CSV files of the batch processing assistant. Fields
/// are quoted according to RFC 4180, but the separator is a vertical bar, so
/// that the files open nicely in spreadsheet applications regardless of the
/// list separator of the user's locale.
/// </summary>
public static class BatchProcessingCsv
{
    public const char SEPARATOR = '|';

    public static string ToCsvRow(params string[] fields) => string.Join(SEPARATOR, fields.Select(ToCsvField));

    /// <summary>
    /// Quotes one CSV field according to RFC 4180.
    /// </summary>
    private static string ToCsvField(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (!text.Contains(SEPARATOR) && !text.Contains('"') && !text.Contains('\n') && !text.Contains('\r'))
            return text;

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    /// <summary>
    /// Parses a CSV text which was written by <see cref="ToCsvRow"/>.
    /// </summary>
    /// <remarks>
    /// We parse the file ourselves instead of splitting lines, because quoted
    /// fields may contain the separator and line breaks.
    /// </remarks>
    public static List<List<string>> Parse(string content)
    {
        var rows = new List<List<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var isQuoted = false;
        var hasContent = false;

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

                case SEPARATOR:
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
    }
}