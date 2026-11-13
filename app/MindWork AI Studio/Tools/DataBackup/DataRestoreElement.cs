namespace AIStudio.Tools.DataBackup;

/// <summary>
/// One element which a restore replaces, as the list of the backup contents names it.
/// </summary>
/// <param name="Location">The folder in which the element lies.</param>
/// <param name="RelativePath">The path of the element within that folder, with forward slashes. A folder ends with a slash.</param>
/// <param name="Keep">The paths below the element which stay as they are, relative to the same folder: the plugins which come with AI Studio or from the organization, for example. The runtime moves them from the replaced element into the new one.</param>
public sealed record DataRestoreElement(DataBackupLocation Location, string RelativePath, IReadOnlyList<string> Keep);