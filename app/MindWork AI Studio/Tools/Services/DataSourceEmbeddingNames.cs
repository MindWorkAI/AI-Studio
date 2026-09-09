namespace AIStudio.Tools.Services;

internal static class DataSourceEmbeddingNames
{
    public static string GetCollectionName(string dataSourceId)
    {
        if (!Guid.TryParse(dataSourceId, out var parsedDataSourceId))
            throw new ArgumentException("Data source ID must be a valid GUID.", nameof(dataSourceId));

        return $"rag_{parsedDataSourceId:N}";
    }
}
