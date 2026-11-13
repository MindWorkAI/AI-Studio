namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Describes the contents of an archive before anything is imported.
/// </summary>
/// <param name="Success">Whether the archive could be read.</param>
/// <param name="Manifest">The manifest of the archive.</param>
/// <param name="Issue">The reason why the archive could not be read, if it could not.</param>
public sealed record ChatArchiveImportPreview(bool Success, ChatArchiveManifest Manifest, string Issue);