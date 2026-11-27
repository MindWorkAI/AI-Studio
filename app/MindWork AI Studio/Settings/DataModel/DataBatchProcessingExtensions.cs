namespace AIStudio.Settings.DataModel;

public static class DataBatchProcessingExtensions
{
    /// <param name="settings">The settings of the Batch Processing Assistant.</param>
    extension(DataBatchProcessing settings)
    {
        /// <summary>
        /// Whether a configuration plugin locks the lower bound of the pause between two files.
        /// </summary>
        public static bool IsMinimumPauseLocked => ManagedConfiguration.TryGet(x => x.BatchProcessing, x => x.MinimumDelaySeconds, out var meta) && meta.IsLocked;

        /// <summary>
        /// Whether a configuration plugin locks the upper bound of the pause between two files.
        /// </summary>
        public static bool IsMaximumPauseLocked => ManagedConfiguration.TryGet(x => x.BatchProcessing, x => x.MaximumDelaySeconds, out var meta) && meta.IsLocked;
    }
}