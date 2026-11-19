namespace AIStudio.Assistants.BatchProcessing;

public static class BatchProcessingInputKindExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BatchProcessingInputKindExtensions).Namespace, nameof(BatchProcessingInputKindExtensions));

    /// <param name="kind">What the batch run goes through.</param>
    extension(BatchProcessingInputKind kind)
    {
        public string Name => kind switch
        {
            BatchProcessingInputKind.FOLDER => TB("A folder with documents"),
            BatchProcessingInputKind.TABLE => TB("A table, row by row or column by column"),

            _ => TB("Unknown"),
        };
    }
}