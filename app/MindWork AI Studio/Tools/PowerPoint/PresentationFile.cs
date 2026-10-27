namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Creates a presentation file from a template and a slide deck.
/// </summary>
/// <remarks>
/// The presentation comes about in a copy of the template in the temporary folder, and only the
/// complete presentation is streamed into the file the user chose. Nothing is created next to that
/// file: inside the Flatpak, the save dialog grants access to exactly the chosen file, and to nothing
/// next to it. A failure before the presentation is complete therefore leaves the target untouched,
/// and neither the template nor the presentation has to be held in memory.
/// </remarks>
internal static class PresentationFile
{
    /// <summary>
    /// The folder below the temporary folder of the system, where the presentations come about.
    /// </summary>
    private const string TEMPORARY_DIRECTORY = "mindwork-ai-studio-presentations";

    /// <summary>
    /// Writes the slide deck into a copy of the template, and the result into the target.
    /// </summary>
    /// <remarks>
    /// The template is read once more rather than reused: it may have changed since the slides were
    /// planned, and the writer has to see the layouts of the very file it edits.
    /// </remarks>
    /// <param name="templatePath">The path of the template, a .pptx or a .potx.</param>
    /// <param name="targetPath">The path of the file the user chose. It may exist; the save dialog asked about overwriting it.</param>
    /// <param name="deck">The slide deck, as checked against the template when it was planned.</param>
    /// <param name="basis">What the deck was planned on, as PresentationTemplate.DescribeBasisOf told it then.</param>
    /// <param name="options">What the user chose for the presentation as a whole.</param>
    /// <param name="pictures">Where the pictures the deck refers to come from.</param>
    /// <param name="logger">Where to report a temporary file which cannot be removed.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Whether the presentation was written, and which pictures it lacks.</returns>
    public static async Task<PresentationFileResult> CreateAsync(string templatePath, string targetPath, SlideDeckSpec deck, IReadOnlyDictionary<string, string> basis, PresentationWriteOptions options, IPictureSource pictures, ILogger logger, CancellationToken token)
    {
        var temporaryDirectory = Path.Join(Path.GetTempPath(), TEMPORARY_DIRECTORY);
        Directory.CreateDirectory(temporaryDirectory);

        var temporaryPath = Path.Join(temporaryDirectory, $"{Guid.NewGuid():N}.pptx");
        try
        {
            await using (var templateFile = new FileStream(templatePath, FileMode.Open, FileAccess.Read, PresentationTemplate.FILE_SHARE))
            await using (var copy = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await templateFile.CopyToAsync(copy, token);

            PresentationWriteResult written;
            await using (var presentationFile = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var currentTemplate = PresentationTemplate.Read(presentationFile);
                presentationFile.Position = 0;
                if (!SlideDeckSpecValidator.StillFits(deck, currentTemplate, basis))
                    return PresentationFileResult.TEMPLATE_CHANGED;

                written = await PresentationWriter.WriteAsync(presentationFile, currentTemplate, deck, options, pictures, token);
            }

            await CopyIntoTargetAsync(temporaryPath, targetPath, logger, token);
            return new PresentationFileResult(true, written.SkippedPictures);
        }
        finally
        {
            DeleteQuietly(temporaryPath, logger);
        }
    }

    /// <summary>
    /// Streams the complete presentation into the file the user chose.
    /// </summary>
    /// <remarks>
    /// The file is written over rather than removed and created anew, which the Flatpak would not
    /// allow. Once writing has begun, a failure removes the file: a half-written presentation is
    /// worse than none. A file which cannot even be opened, e.g., because PowerPoint shows it, stays
    /// as it is.
    /// </remarks>
    private static async Task CopyIntoTargetAsync(string presentationPath, string targetPath, ILogger logger, CancellationToken token)
    {
        await using var presentation = new FileStream(presentationPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        try
        {
            await using (target)
                await presentation.CopyToAsync(target, token);
        }
        catch
        {
            DeleteQuietly(targetPath, logger);
            throw;
        }
    }

    /// <summary>
    /// Removes a file which is of no use anymore, and only reports when that fails.
    /// </summary>
    /// <remarks>
    /// A copy of the template left behind would pile up in the temporary folder with every attempt.
    /// </remarks>
    private static void DeleteQuietly(string path, ILogger logger)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "The file '{FilePath}' cannot be deleted.", path);
        }
    }
}