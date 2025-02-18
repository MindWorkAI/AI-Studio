namespace AIStudio.Settings.DataModel;

public sealed class DataAgentDataSourceSelection
{
    /// <summary>
    /// Preselect any data source selection options?
    /// </summary>
    public bool PreselectAgentOptions { get; set; }
    
    /// <summary>
    /// Preselect a data source selection provider?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = string.Empty;
}