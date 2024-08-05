namespace AIStudio.Settings.DataModel;

public sealed class DataTextContentCleaner
{
    /// <summary>
    /// Preselect any text content cleaner options?
    /// </summary>
    public bool PreselectAgentOptions { get; set; }
    
    /// <summary>
    /// Preselect a text content cleaner provider?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = string.Empty;
}