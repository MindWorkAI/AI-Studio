using AIStudio.Provider;

namespace AIStudio.Settings.DataModel.PreviousModels;

/// <summary>
/// The settings of the text content cleaner, as settings files up to version V6 store them.
/// </summary>
public sealed class DataTextContentCleanerV6
{
    /// <summary>
    /// Preselect any text content cleaner options?
    /// </summary>
    public bool PreselectAgentOptions { get; set; }

    /// <summary>
    /// Preselect a text content cleaner provider?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = string.Empty;

    /// <summary>
    /// The minimum confidence level of the provider which cleans the text content.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
}