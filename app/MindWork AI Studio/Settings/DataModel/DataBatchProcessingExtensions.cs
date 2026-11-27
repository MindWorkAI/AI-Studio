using AIStudio.Tools.Pauses;

namespace AIStudio.Settings.DataModel;

public static class DataBatchProcessingExtensions
{
    /// <param name="settings">The settings of the Batch Processing Assistant.</param>
    extension(DataBatchProcessing settings)
    {
        /// <summary>
        /// The bounds of the pause between two files which the settings preset, as the user set them
        /// and as configuration plugins lock them, cf. PauseRange.GetEffective.
        /// </summary>
        public PauseRange EffectivePauseRange => PauseRange.GetEffective(
            DataBatchProcessing.PAUSE_LIMITS,
            settings.MinimumDelaySeconds,
            () => ManagedConfiguration.TryGet(x => x.BatchProcessing, x => x.MinimumDelaySeconds, out var meta) && meta.IsLocked,
            settings.MaximumDelaySeconds,
            () => ManagedConfiguration.TryGet(x => x.BatchProcessing, x => x.MaximumDelaySeconds, out var meta) && meta.IsLocked);
    }
}