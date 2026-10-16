namespace AIStudio.Assistants.BatchProcessing;

public static class BatchProcessingPromptSourceExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BatchProcessingPromptSourceExtensions).Namespace, nameof(BatchProcessingPromptSourceExtensions));

    public static string Name(this BatchProcessingPromptSource promptSource) => promptSource switch
    {
        BatchProcessingPromptSource.FREE_PROMPT => TB("Use a free prompt"),
        BatchProcessingPromptSource.POLICY => TB("Use a document analysis policy"),
        BatchProcessingPromptSource.FILE_IMPORT => TB("Import from a file (.md)"),

        _ => TB("Unknown prompt source"),
    };
}