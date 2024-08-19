using AIStudio.Assistants.IconFinder;

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
    /// The preselected icon provider.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}