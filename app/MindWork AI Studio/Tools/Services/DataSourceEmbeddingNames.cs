namespace AIStudio.Tools.Services;

internal static class DataSourceEmbeddingNames
{
    public static string GetCollectionName(string dataSourceName, string dataSourceId)
    {
        var safeId = dataSourceId
            .ToLowerInvariant()
            .Replace("-", string.Empty, StringComparison.Ordinal);

        var safeName = new string(dataSourceName
            .ToLowerInvariant()
            .Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9')
            .Take(32)
            .ToArray());

        safeName = string.IsNullOrWhiteSpace(safeName) ? "datasource" : safeName;

        return $"rag_{safeName}_{safeId}";
    }
}
