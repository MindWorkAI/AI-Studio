using AIStudio.Tools.Pauses;

namespace AIStudio.Settings.DataModel;

public static class DataDataSourceIndexingExtensions
{
    /// <param name="settings">The settings of the indexing.</param>
    extension(DataDataSourceIndexing settings)
    {
        /// <summary>
        /// Whether a configuration plugin locks the lower bound of the pause between two indexing steps.
        /// </summary>
        public static bool IsMinimumPauseLocked => ManagedConfiguration.TryGet(x => x.App.DataSourceIndexing, x => x.MinimumPauseBetweenStepsSeconds, out var meta) && meta.IsLocked;

        /// <summary>
        /// Whether a configuration plugin locks the upper bound of the pause between two indexing steps.
        /// </summary>
        public static bool IsMaximumPauseLocked => ManagedConfiguration.TryGet(x => x.App.DataSourceIndexing, x => x.MaximumPauseBetweenStepsSeconds, out var meta) && meta.IsLocked;

        /// <summary>
        /// The bounds of the pause between two indexing steps which apply now, as the user set them
        /// and as configuration plugins lock them, cf. PauseRange.GetEffective.
        /// </summary>
        public PauseRange EffectivePauseRange => PauseRange.GetEffective(
            DataDataSourceIndexing.PAUSE_LIMITS,
            settings.MinimumPauseBetweenStepsSeconds,
            () => DataDataSourceIndexing.IsMinimumPauseLocked,
            settings.MaximumPauseBetweenStepsSeconds,
            () => DataDataSourceIndexing.IsMaximumPauseLocked);
    }
}