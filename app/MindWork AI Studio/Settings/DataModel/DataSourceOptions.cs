namespace AIStudio.Settings.DataModel;

public sealed class DataSourceOptions
{
    /// <summary>
    /// Whether data sources are disabled in this context.
    /// </summary>
    public bool DisableDataSources { get; set; } = true;
    
    /// <summary>
    /// Whether the data sources should be selected automatically.
    /// </summary>
    /// <remarks>
    /// When true, the appropriate data sources for the current prompt are
    /// selected automatically. When false, the user has to select the
    /// data sources manually.
    ///
    /// This setting does not affect the selection of the actual data
    /// for augmentation.
    /// </remarks>
    public bool AutomaticDataSourceSelection { get; set; }
    
    /// <summary>
    /// Whether the retrieved data should be validated for the current prompt.
    /// </summary>
    /// <remarks>
    /// When true, the retrieved data is validated against the current prompt.
    /// An AI will decide whether the data point is useful for answering the
    /// prompt or not.
    /// </remarks>
    public bool AutomaticValidation { get; set; }

    /// <summary>
    /// The preselected data source IDs. When these data sources are available
    /// for the selected provider, they are pre-selected.
    /// </summary>
    public List<string> PreselectedDataSourceIds { get; set; } = [];

    /// <summary>
    /// Returns true when data sources are enabled.
    /// </summary>
    /// <returns>True when data sources are enabled.</returns>
    public bool IsEnabled()
    {
        if(this.DisableDataSources)
            return false;
        
        if(this.AutomaticDataSourceSelection)
            return true;
        
        return this.PreselectedDataSourceIds.Count > 0;
    }

    /// <summary>
    /// Creates a copy of the current data source options.
    /// </summary>
    /// <returns>A copy of the current data source options.</returns>
    public DataSourceOptions CreateCopy() => new()
    {
        DisableDataSources = this.DisableDataSources,
        AutomaticDataSourceSelection = this.AutomaticDataSourceSelection,
        AutomaticValidation = this.AutomaticValidation,
        PreselectedDataSourceIds = [..this.PreselectedDataSourceIds],
    };
}