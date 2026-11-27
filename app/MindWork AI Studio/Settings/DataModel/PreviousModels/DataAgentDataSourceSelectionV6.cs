using AIStudio.Provider;

namespace AIStudio.Settings.DataModel.PreviousModels;

/// <summary>
/// The settings of the data source selection, as settings files up to version V6 store them.
/// </summary>
public sealed class DataAgentDataSourceSelectionV6
{
    /// <summary>
    /// Preselect any data source selection options?
    /// </summary>
    public bool PreselectAgentOptions { get; set; }

    /// <summary>
    /// Preselect a data source selection provider?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = string.Empty;

    /// <summary>
    /// The minimum confidence level of the provider which selects the data sources.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
}