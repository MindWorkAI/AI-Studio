using AIStudio.Provider;

namespace AIStudio.Settings.DataModel.PreviousModels;

/// <summary>
/// The settings of the legal check assistant, as settings files up to version V6 store them.
/// </summary>
public sealed class DataLegalCheckV6
{
    /// <summary>
    /// Do you want to preselect any legal check options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// Hide the web content reader?
    /// </summary>
    public bool HideWebContentReader { get; set; }

    /// <summary>
    /// Preselect the web content reader?
    /// </summary>
    public bool PreselectWebContentReader { get; set; }

    /// <summary>
    /// Preselect the text content cleaner?
    /// </summary>
    public bool PreselectContentCleanerAgent { get; set; }

    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;

    /// <summary>
    /// The preselected provider.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;

    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
}