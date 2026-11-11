using AIStudio.Tools.Security;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Web;

/// <summary>
/// Reads the text of a document downloaded from the web, through the runtime.
/// </summary>
/// <remarks>
/// The runtime reads PDFs, Word and OpenDocument texts, presentations, and spreadsheets. It
/// recognizes the format by the content, refuses programs whatever they are called, and filters
/// prompt injections while it streams the text. What it removed is reported with the action the
/// document is read for, naming the address of the document.
/// </remarks>
/// <param name="rustService">The runtime.</param>
/// <param name="logger">The logger.</param>
/// <param name="files">Where documents are downloaded to, or null for the data directory of AI Studio.</param>
public sealed class WebDocumentReader(RustService rustService, ILogger<WebDocumentReader> logger, WebDocumentFiles? files = null)
{
    /// <summary>
    /// The UTC ticks from which the next download deletes what earlier ones left behind.
    /// </summary>
    /// <remarks>
    /// Only a crash leaves files behind, so the first download after the start looks for them,
    /// and later ones once per WebDocumentFiles.LEFTOVER_AGE at most.
    /// </remarks>
    private long nextLeftoverCleanupTicks;

    /// <summary>
    /// Makes up the path of a file for one document, after deleting what earlier downloads left behind, when that is due.
    /// </summary>
    /// <exception cref="InvalidOperationException">The data directory is not known yet.</exception>
    public string CreatePath()
    {
        var documentFiles = files ?? WebDocumentFiles.InDataDirectory() ?? throw new InvalidOperationException("The data directory is not known yet.");
        var now = DateTime.UtcNow.Ticks;
        var due = Interlocked.Read(ref this.nextLeftoverCleanupTicks);

        // Only the download which moves the date on cleans up, so parallel ones do not race for the same files:
        if (now >= due && Interlocked.CompareExchange(ref this.nextLeftoverCleanupTicks, now + WebDocumentFiles.LEFTOVER_AGE.Ticks, due) == due)
            documentFiles.DeleteLeftovers(logger);

        return documentFiles.CreatePath();
    }

    /// <summary>
    /// Reads the text of a downloaded document.
    /// </summary>
    /// <param name="path">The file the document was downloaded to.</param>
    /// <param name="source">The address the document came from, as the user hears of it.</param>
    /// <param name="maxCharacters">How many characters of its text are kept at most.</param>
    /// <param name="reportingScope">The action the document is read for, or null to report what the filter removed right away.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The result of reading the document.</returns>
    public Task<FileExtractionResult> ReadAsync(string path, Uri source, int maxCharacters, PromptInjectionReportingScope? reportingScope, CancellationToken token) =>
        rustService.ReadArbitraryFileData(path, int.MaxValue, reportAs: PromptInjectionSource.WebContent(source.ToString()), reportingScope: reportingScope, token: token, maxCharacters: maxCharacters);

    /// <summary>
    /// Deletes the file of a document, should it exist.
    /// </summary>
    /// <param name="path">The path from CreatePath.</param>
    public void Delete(string path) => WebDocumentFiles.Delete(path, logger);
}