namespace AIStudio.Tools.Services;

public sealed record DataSourceEmbeddingOverview(bool IsVisible, DataSourceEmbeddingState State, int IndexedFiles, int TotalFiles, int FailedFiles);
