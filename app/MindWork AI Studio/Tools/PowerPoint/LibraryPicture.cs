namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// A picture of a picture library, which the LLM may choose by its name.
/// </summary>
/// <param name="FilePath">The full path of the file.</param>
/// <param name="Name">The path below the library folder, with forward slashes, e.g., "icons/rocket.png"; this is what the LLM reads.</param>
/// <param name="IsIcon">Whether the picture is an icon, because its name or the name of one of its folders starts with "icon".</param>
internal sealed record LibraryPicture(string FilePath, string Name, bool IsIcon);
