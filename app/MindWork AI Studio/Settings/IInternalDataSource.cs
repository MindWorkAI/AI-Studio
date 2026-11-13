namespace AIStudio.Settings;

/// <summary>
/// A data source in DataSources whose content AI Studio embeds and indexes itself.
/// </summary>
public interface IInternalDataSource : IDataSource, IIndexedDataSource;
