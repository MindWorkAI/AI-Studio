namespace AIStudio.Assistants.BatchProcessing;

public static class BatchProcessingTableFormatExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BatchProcessingTableFormatExtensions).Namespace, nameof(BatchProcessingTableFormatExtensions));

    /// <param name="format">The format of the results table.</param>
    extension(BatchProcessingTableFormat format)
    {
        public string Name => format switch
        {
            BatchProcessingTableFormat.CSV => TB("CSV file"),
            BatchProcessingTableFormat.EXCEL => TB("Microsoft Excel workbook (.xlsx)"),

            _ => TB("Unknown"),
        };

        /// <summary>
        /// The extension of the results table, with its dot.
        /// </summary>
        public string FileExtension => format switch
        {
            BatchProcessingTableFormat.EXCEL => ".xlsx",

            _ => ".csv",
        };
    }
}