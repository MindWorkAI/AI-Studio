using AIStudio.Settings;

namespace AIStudio.Tools.RAG;

/// <summary>
/// Result of any data selection process.
/// </summary>
/// <param name="ProceedWithRAG">Makes it sense to proceed with the RAG process?</param>
/// <param name="SelectedDataSources">The selected data sources.</param>
public readonly record struct DataSelectionResult(bool ProceedWithRAG, IReadOnlyList<IDataSource> SelectedDataSources);