namespace AIStudio.Tools.Services;

public sealed record DataSourceEmbeddingOverview(DataSourceEmbeddingState State, int IndexedFiles, int TotalFiles, int FailedFiles);
