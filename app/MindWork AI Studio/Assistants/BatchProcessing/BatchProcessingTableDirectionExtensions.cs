namespace AIStudio.Assistants.BatchProcessing;

public static class BatchProcessingTableDirectionExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BatchProcessingTableDirectionExtensions).Namespace, nameof(BatchProcessingTableDirectionExtensions));

    /// <param name="direction">How the batch run goes through the table.</param>
    extension(BatchProcessingTableDirection direction)
    {
        public string Name => direction switch
        {
            BatchProcessingTableDirection.ROWS => TB("Each row is one document"),
            BatchProcessingTableDirection.COLUMNS => TB("Each column is one document"),

            _ => TB("Unknown"),
        };
    }
}