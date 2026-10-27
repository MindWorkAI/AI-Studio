namespace AIStudio.Tools.Services;

public sealed record DataSourceEmbeddingOverview(DataSourceEmbeddingState State, int IndexedDocuments, int TotalDocuments,
    int FailedDocuments, DataSourceEmbeddingHealth Health, int AttentionDataSources)
{
    /// <summary>
    /// Reduces the individual statuses to the activity and health shown by the navigation entry.
    /// </summary>
    public static DataSourceEmbeddingOverview Create(IReadOnlyList<DataSourceEmbeddingStatus> statuses)
    {
        var activeStatus = statuses.FirstOrDefault(status => status.State is DataSourceEmbeddingState.RUNNING)
            ?? statuses.FirstOrDefault(status => status.State is DataSourceEmbeddingState.QUEUED);
        var health = GetHealth(statuses);
        var attentionDataSources = statuses.Count(status => status.Health is not DataSourceEmbeddingHealth.HEALTHY);

        if (activeStatus is not null)
        {
            return new(
                activeStatus.State,
                activeStatus.IndexedDocuments,
                Math.Max(activeStatus.TotalDocuments, 1),
                activeStatus.FailedDocuments,
                health,
                attentionDataSources);
        }

        return new(
            health is DataSourceEmbeddingHealth.ERROR ? DataSourceEmbeddingState.FAILED : DataSourceEmbeddingState.COMPLETED,
            statuses.Sum(status => status.IndexedDocuments),
            statuses.Sum(status => status.TotalDocuments),
            statuses.Sum(status => status.FailedDocuments),
            health,
            attentionDataSources);
    }

    private static DataSourceEmbeddingHealth GetHealth(IReadOnlyList<DataSourceEmbeddingStatus> statuses)
    {
        if (statuses.Any(status => status.Health is DataSourceEmbeddingHealth.ERROR))
            return DataSourceEmbeddingHealth.ERROR;

        return statuses.Any(status => status.Health is DataSourceEmbeddingHealth.WARNING)
            ? DataSourceEmbeddingHealth.WARNING
            : DataSourceEmbeddingHealth.HEALTHY;
    }
}
