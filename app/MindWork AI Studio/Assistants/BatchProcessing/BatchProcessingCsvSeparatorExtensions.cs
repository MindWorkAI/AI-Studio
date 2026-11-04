namespace AIStudio.Assistants.BatchProcessing;

public static class BatchProcessingCsvSeparatorExtensions
{
    private const char DEFAULT_SEPARATOR = ';';

    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BatchProcessingCsvSeparatorExtensions).Namespace, nameof(BatchProcessingCsvSeparatorExtensions));

    extension(BatchProcessingCsvSeparator separator)
    {
        public string Name => separator switch
        {
            BatchProcessingCsvSeparator.COMMA => TB("Comma (,)"),
            BatchProcessingCsvSeparator.SEMICOLON => TB("Semicolon (;)"),
            BatchProcessingCsvSeparator.PIPE => TB("Vertical bar (|)"),
            BatchProcessingCsvSeparator.TAB => TB("Tab"),
            BatchProcessingCsvSeparator.CUSTOM => TB("Custom character"),

            _ => TB("Unknown"),
        };

        public char Character(string customSeparator) => separator switch
        {
            BatchProcessingCsvSeparator.COMMA => ',',
            BatchProcessingCsvSeparator.SEMICOLON => ';',
            BatchProcessingCsvSeparator.PIPE => '|',
            BatchProcessingCsvSeparator.TAB => '\t',
            BatchProcessingCsvSeparator.CUSTOM when IsValidCustomSeparator(customSeparator) => customSeparator[0],

            _ => DEFAULT_SEPARATOR,
        };
    }

    internal static bool IsValidCustomSeparator(string separator)
    {
        if (string.IsNullOrEmpty(separator) || separator.Length is not 1)
            return false;

        var character = separator[0];
        return !char.IsLetterOrDigit(character)
               && !char.IsWhiteSpace(character)
               && character is not '"' and not '\r' and not '\n';
    }
}