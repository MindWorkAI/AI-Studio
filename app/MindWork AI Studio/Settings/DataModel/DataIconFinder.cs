using AIStudio.Assistants.IconFinder;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataIconFinder
{
    /// <summary>
    /// Do we want to preselect any icon options?
    /// </summary>
    public bool PreselectOptions { get; set; }
    
    /// <summary>
    /// The preselected icon source.
    /// </summary>
    public IconSources PreselectedSource { get; set; }

    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;

    /// <summary>
    /// The preselected icon provider.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}