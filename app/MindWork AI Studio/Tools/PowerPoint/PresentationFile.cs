using AIStudio.Tools.OpenXml;

namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Creates a presentation file from a template and a slide deck.
/// </summary>
/// <remarks>
/// The presentation comes about in a TemplateCopy, and only the complete presentation is streamed
/// into the file the user chose; TemplateCopy says why.
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
        using var copy = await TemplateCopy.CreateAsync(templatePath, TEMPORARY_DIRECTORY, ".pptx", logger, token);

        PresentationWriteResult written;
        await using (var presentationFile = copy.OpenForEditing())
        {
            var currentTemplate = PresentationTemplate.Read(presentationFile);
            presentationFile.Position = 0;
            if (!SlideDeckSpecValidator.StillFits(deck, currentTemplate, basis))
                return PresentationFileResult.TEMPLATE_CHANGED;

            written = await PresentationWriter.WriteAsync(presentationFile, currentTemplate, deck, options, pictures, token);
        }

        await copy.WriteIntoAsync(targetPath, token);
        return new PresentationFileResult(true, written.SkippedPictures);
    }
}