namespace AIStudio.Tools.OpenXml;

/// <summary>
/// A copy of a template in the temporary folder, in which an Office document comes about before it
/// goes into the file the user chose.
/// </summary>
/// <remarks>
/// Only the complete document is streamed into the file the user chose. Nothing is created next to
/// that file: inside the Flatpak, the save dialog grants access to exactly the chosen file, and to
/// nothing next to it. A failure before the document is complete therefore leaves the target
/// untouched, and neither the template nor the document has to be held in memory. Disposing the
/// copy removes it.
/// </remarks>
internal sealed class TemplateCopy : IDisposable
{
    private readonly ILogger logger;

    private TemplateCopy(string filePath, ILogger logger)
    {
        this.FilePath = filePath;
        this.logger = logger;
    }

    /// <summary>
    /// The path of the copy.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Copies the template into a folder below the temporary folder of the system.
    /// </summary>
    /// <param name="templatePath">The path of the template.</param>
    /// <param name="temporaryDirectory">The name of the folder below the temporary folder, one per
    /// kind of document.</param>
    /// <param name="fileExtension">The extension of the document which comes about, including the
    /// leading dot.</param>
    /// <param name="logger">Where to report a temporary file which cannot be removed.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The copy.</returns>
    public static async Task<TemplateCopy> CreateAsync(string templatePath, string temporaryDirectory, string fileExtension, ILogger logger, CancellationToken token)
    {
        var directory = Path.Join(Path.GetTempPath(), temporaryDirectory);
        Directory.CreateDirectory(directory);

        var copy = new TemplateCopy(Path.Join(directory, $"{Guid.NewGuid():N}{fileExtension}"), logger);
        try
        {
            await using var templateFile = TemplateFile.OpenRead(templatePath);
            await using var copyFile = new FileStream(copy.FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await templateFile.CopyToAsync(copyFile, token);
        }
        catch
        {
            copy.Dispose();
            throw;
        }

        return copy;
    }

    /// <summary>
    /// Opens the copy for the writer to edit it.
    /// </summary>
    /// <returns>The copy, opened for reading and writing.</returns>
    public FileStream OpenForEditing() => new(this.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    /// <summary>
    /// Streams the complete document into the file the user chose.
    /// </summary>
    /// <remarks>
    /// The file is written over rather than removed and created anew, which the Flatpak would not
    /// allow. Once writing has begun, a failure removes the file: a half-written document is worse
    /// than none. A file which cannot even be opened, e.g., because PowerPoint or Word shows it, stays
    /// as it is.
    /// </remarks>
    /// <param name="targetPath">The path of the file the user chose. It may exist; the save dialog
    /// asked about overwriting it.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task WriteIntoAsync(string targetPath, CancellationToken token)
    {
        await using var document = new FileStream(this.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        try
        {
            await using (target)
                await document.CopyToAsync(target, token);
        }
        catch
        {
            this.DeleteQuietly(targetPath);
            throw;
        }
    }

    /// <summary>
    /// Removes the copy.
    /// </summary>
    /// <remarks>
    /// A copy of the template left behind would pile up in the temporary folder with every attempt.
    /// </remarks>
    public void Dispose() => this.DeleteQuietly(this.FilePath);

    /// <summary>
    /// Removes a file which is of no use anymore, and only reports when that fails.
    /// </summary>
    private void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "The file '{FilePath}' cannot be deleted.", path);
        }
    }
}