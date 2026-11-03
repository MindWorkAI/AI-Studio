using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tools;

/// <summary>
/// What the user can do with the document a source names: open it, or show it in the file manager.
/// </summary>
/// <remarks>
/// The list of sources and the passages of a source both offer it, and a failure reaches the user
/// the same way from either of them.
/// </remarks>
public static class SourceDocumentLocationExtensions
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(SourceDocumentLocationExtensions));

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SourceDocumentLocationExtensions).Namespace, nameof(SourceDocumentLocationExtensions));

    /// <summary>
    /// Opens the document in the program the system uses for it, at its page.
    /// </summary>
    /// <remarks>
    /// Whether the program can be sent to a page is the runtime's business, and it says afterwards
    /// whether it managed to. Nothing is shown about that: the document is open, and the page is
    /// named where the user asked to open it.
    /// </remarks>
    /// <param name="document">The document to open, and the page to show.</param>
    /// <param name="rustService">The runtime, which opens the document.</param>
    public static async Task OpenAsync(this SourceDocumentLocation document, RustService rustService)
    {
        OpenDocumentResponse response;
        try
        {
            response = await rustService.TryOpenDocumentInSystemViewer(document.Path, document.PageNumber);
        }
        catch (Exception e)
        {
            LOGGER.LogWarning(e, "Could not open a source document.");
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Description, TB("Could not open the document.")));
            return;
        }

        if (response.Success)
            return;

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? TB("Unknown error") : response.Issue;
        await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Description, string.Format(TB("Could not open the document: {0}"), issue)));
    }

    /// <summary>
    /// Opens the file browser of the system and selects the document in it.
    /// </summary>
    /// <remarks>
    /// The second way out: a document which the system opens in the wrong program, or which the user
    /// wants to move or send on instead of read, is reached from here without being opened. This is
    /// the same way out the embeddings page offers for a file it could not read.
    /// </remarks>
    /// <param name="document">The document to show.</param>
    /// <param name="rustService">The runtime, which opens the file browser.</param>
    public static async Task ShowInFileManagerAsync(this SourceDocumentLocation document, RustService rustService)
    {
        OpenPathResponse response;
        try
        {
            response = await rustService.TryOpenPathInRuntimeFileManager(document.Path);
        }
        catch (Exception e)
        {
            LOGGER.LogWarning(e, "Could not show a source document in the file manager.");
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.FolderOpen, TB("Could not open the file location.")));
            return;
        }

        if (response.Success)
            return;

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? TB("Unknown error") : response.Issue;
        await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.FolderOpen, string.Format(TB("Could not open the file location: {0}"), issue)));
    }
}