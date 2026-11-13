namespace AIStudio.Tools.Services;

/// <summary>
/// Folders which the exclusion list of a local data source left out, summarized by name, e.g.,
/// every folder named bin. The user sees whether the list caught a folder they wanted, such as
/// a folder named Release which holds press releases.
/// </summary>
/// <param name="FolderName">The name of the folders.</param>
/// <param name="FolderCount">How many folders of this name were left out.</param>
public sealed record DataSourceEmbeddingExcludedFolderGroup(string FolderName, int FolderCount);