namespace AIStudio.Tools.OpenXml;

/// <summary>
/// Opens the template of an Office document, such as a PowerPoint or a Word template.
/// </summary>
internal static class TemplateFile
{
    /// <summary>
    /// How a template file is opened for reading.
    /// </summary>
    /// <remarks>
    /// Other programs may go on writing or deleting the file meanwhile. PowerPoint and Word keep a
    /// file open for writing as long as they show it, and a template which is still open there has
    /// to be readable all the same.
    /// </remarks>
    private const FileShare FILE_SHARE = FileShare.ReadWrite | FileShare.Delete;

    /// <summary>
    /// Opens the template file at the given path for reading.
    /// </summary>
    /// <remarks>
    /// Throws when the file is missing or cannot be opened; the caller decides what the user hears
    /// about it.
    /// </remarks>
    /// <param name="path">The path of the template file.</param>
    /// <returns>The file, opened for reading.</returns>
    public static FileStream OpenRead(string path) => new(path, FileMode.Open, FileAccess.Read, FILE_SHARE);
}