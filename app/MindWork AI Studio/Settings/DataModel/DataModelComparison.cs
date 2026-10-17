namespace AIStudio.Settings.DataModel;

public sealed class DataModelComparison
{
    /// <summary>
    /// The user's saved presets for the model comparison assistant.
    /// </summary>
    public List<ModelComparisonPreset> Presets { get; set; } = [];
}
