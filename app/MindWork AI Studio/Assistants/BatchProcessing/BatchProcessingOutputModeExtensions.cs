namespace AIStudio.Assistants.BatchProcessing;

public static class BatchProcessingOutputModeExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BatchProcessingOutputModeExtensions).Namespace, nameof(BatchProcessingOutputModeExtensions));

    public static string Name(this BatchProcessingOutputMode outputMode) => outputMode switch
    {
        BatchProcessingOutputMode.INDIVIDUAL_FILES => TB("One file per document"),
        BatchProcessingOutputMode.TABLE_ONLY => TB("One CSV results table, where each answer becomes one row"),

        _ => TB("Unknown output mode"),
    };
}