namespace AIStudio.Settings.DataModel;

public sealed class DataDataSourceIndexing
{
    /// <summary>
    /// Whether local data source embeddings should refresh automatically when files change.
    /// </summary>
    public bool AutomaticRefresh { get; set; } = true;
}
