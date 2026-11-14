using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Decides which preset the mode Answer Variance works in, since it always works in one.
/// </summary>
public static class ModelComparisonPresetChoice
{
    /// <summary>
    /// Chooses the preset the mode Answer Variance opens with, and the one it continues with after a preset was deleted: the
    /// preselected preset of the options, or else the first one.
    /// </summary>
    /// <remarks>
    /// An assistant session which the page restores brings back the preset of its own instead, because the form it
    /// restores belongs to that preset.
    /// </remarks>
    /// <param name="presets">The presets, in the order the list shows them.</param>
    /// <param name="options">The options of the assistant.</param>
    /// <returns>The ID of the preset, or null when there is none, and a new one is needed.</returns>
    public static Guid? Choose(IReadOnlyList<ModelComparisonPreset> presets, DataModelComparison options)
    {
        if (options.PreselectOptions
            && Guid.TryParse(options.PreselectedPresetId, out var preselectedId)
            && presets.Any(preset => preset.Id == preselectedId))
            return preselectedId;

        return presets.Count > 0 ? presets[0].Id : null;
    }
}