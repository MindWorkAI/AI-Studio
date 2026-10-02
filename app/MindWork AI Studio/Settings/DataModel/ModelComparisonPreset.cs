namespace AIStudio.Settings.DataModel;

/// <summary>
/// A saved set of model comparison inputs, so a familiar test case does not have to be typed and
/// picked by hand again every time.
/// </summary>
/// <remarks>
/// Purely local to this user: unlike <see cref="DataDocumentAnalysisPolicy"/>, there is no Lua import
/// path and no enterprise ownership, because nothing here needs to be rolled out centrally.
/// </remarks>
public sealed record ModelComparisonPreset
{
    public required string Id { get; init; }

    /// <summary>
    /// The name shown in the preset picker. Mutable, unlike the rest of a saved preset: renaming is
    /// an edit to a saved entry, while every other field is only ever set once, at the moment the
    /// current form is captured.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The first model's provider ID, or empty when none was selected while saving. Resolved back to
    /// a provider through <see cref="SettingsManager.GetProviderById"/>, which already answers
    /// <see cref="Provider.NONE"/> for an ID that no longer exists.
    /// </summary>
    public string FirstProviderId { get; init; } = string.Empty;

    /// <summary>
    /// The second model's provider ID. See <see cref="FirstProviderId"/>.
    /// </summary>
    public string SecondProviderId { get; init; } = string.Empty;

    public bool JudgeEnabled { get; init; }

    /// <summary>
    /// The judge's provider ID. See <see cref="FirstProviderId"/>.
    /// </summary>
    public string JudgeProviderId { get; init; } = string.Empty;

    public string JudgeInstructions { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string Question { get; init; } = string.Empty;

    public int RunCount { get; init; } = 1;
}
