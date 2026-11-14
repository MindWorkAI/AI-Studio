namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// A saved set of the inputs of the Model Comparison assistant, so that a familiar test case does not have to be typed
/// and picked by hand again every time.
/// </summary>
/// <remarks>
/// Each preset is a file of its own below the data directory, see ModelComparisonStore, not a part of the settings: a
/// preset can carry a long document as its context, and the settings are written anew whenever any setting changes.
/// Presets belong to the user alone; no configuration plugin rolls them out.
/// </remarks>
public sealed record ModelComparisonPreset
{
    /// <summary>
    /// The ID of the preset, which names its file.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name shown in the preset picker.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the provider of the first model, or empty when none was selected while saving. Resolved back to a
    /// provider through SettingsManager.GetProviderById, which answers Provider.NONE for an ID that no longer exists.
    /// </summary>
    public string FirstProviderId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the provider of the second model, see FirstProviderId.
    /// </summary>
    public string SecondProviderId { get; set; } = string.Empty;

    public bool JudgeEnabled { get; set; }

    /// <summary>
    /// The ID of the provider of the judge, see FirstProviderId.
    /// </summary>
    public string JudgeProviderId { get; set; } = string.Empty;

    /// <summary>
    /// The expectations, values, and goals of the user, by which the judge decides, see ModelComparisonJudgeExpectations.
    /// </summary>
    public string JudgeInstructions { get; set; } = string.Empty;

    public string Context { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public int RunCount { get; set; } = 1;
}