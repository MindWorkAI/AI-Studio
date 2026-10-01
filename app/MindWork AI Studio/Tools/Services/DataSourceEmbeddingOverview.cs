namespace AIStudio.Tools.Services;

public sealed record DataSourceEmbeddingOverview(DataSourceEmbeddingState State, int IndexedDocuments, int TotalDocuments, int FailedDocuments);