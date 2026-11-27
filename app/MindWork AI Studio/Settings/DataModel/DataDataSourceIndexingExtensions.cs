using AIStudio.Tools.Pauses;

namespace AIStudio.Settings.DataModel;

public static class DataDataSourceIndexingExtensions
{
    /// <param name="settings">The settings of the indexing.</param>
    extension(DataDataSourceIndexing settings)
    {
        /// <summary>
        /// The bounds of the pause between two indexing steps which apply now, as the user set them
        /// and as configuration plugins lock them, cf. PauseRange.GetEffective.
        /// </summary>
        public PauseRange EffectivePauseRange => PauseRange.GetEffective(
            DataDataSourceIndexing.PAUSE_LIMITS,
            settings.MinimumPauseBetweenStepsSeconds,
            () => ManagedConfiguration.TryGet(x => x.App.DataSourceIndexing, x => x.MinimumPauseBetweenStepsSeconds, out var meta) && meta.IsLocked,
            settings.MaximumPauseBetweenStepsSeconds,
            () => ManagedConfiguration.TryGet(x => x.App.DataSourceIndexing, x => x.MaximumPauseBetweenStepsSeconds, out var meta) && meta.IsLocked);
    }
}