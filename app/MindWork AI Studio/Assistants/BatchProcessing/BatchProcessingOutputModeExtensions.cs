namespace AIStudio.Assistants.BatchProcessing;

public static class BatchProcessingOutputModeExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BatchProcessingOutputModeExtensions).Namespace, nameof(BatchProcessingOutputModeExtensions));

    extension(BatchProcessingOutputMode outputMode)
    {
        public string Name => outputMode switch
        {
            BatchProcessingOutputMode.INDIVIDUAL_FILES => TB("One file per document"),
            BatchProcessingOutputMode.TABLE_ONLY => TB("One results table as CSV or Excel, where each answer becomes one row"),

            _ => TB("Unknown output mode"),
        };
    }
}