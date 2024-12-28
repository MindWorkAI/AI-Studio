using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataERI
{
    /// <summary>
    /// Should we automatically save any input made in the ERI assistant?
    /// </summary>
    public bool AutoSaveChanges { get; set; } = true;

    /// <summary>
    /// Preselect any ERI options?
    /// </summary>
    public bool PreselectOptions { get; set; } = true;

    /// <summary>
    /// Data for the ERI servers.
    /// </summary>
    public List<DataERIServer> ERIServers { get; set; } = new();

    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// Which coding provider should be preselected?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
}