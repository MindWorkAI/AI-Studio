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

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(SourceDocumentLocationExtensions).Namespace, nameof(SourceDocumentLocationExtensions));

    /// <param name="document">The document to show, and the page to show it at.</param>
    extension(SourceDocumentLocation document)
    {
        /// <summary>
        /// Opens the document in the program the system uses for it, at its page.
        /// </summary>
        /// <remarks>
        /// Whether the program can be sent to a page is the runtime's business, and it says afterwards
        /// what became of the page. When the page did not arrive, the user hears where the passage is
        /// and why AI Studio could not do otherwise, see GetPageNotice. Otherwise, they would take the
        /// beginning of the document, or the place their program remembered, for the passage.
        /// </remarks>
        /// <param name="rustService">The runtime, which opens the document.</param>
        public async Task OpenAsync(RustService rustService)
        {
            OpenDocumentResponse response;
            try
            {
                response = await rustService.TryOpenDocumentInSystemViewer(document.Path, document.PageNumber);
            }
            catch (Exception e)
            {
                LOGGER.LogWarning(e, "Could not open a source document.");
                await MessageBus.INSTANCE.SendError(new(AppIcons.DOCUMENT, TB("Could not open the document.")));
                return;
            }

            if (response.Success)
            {
                if (document.GetPageNotice(response) is { } notice)
                    await MessageBus.INSTANCE.SendInfo(new(AppIcons.DOCUMENT, notice));

                return;
            }

            var issue = string.IsNullOrWhiteSpace(response.Issue) ? TB("Unknown error") : response.Issue;
            await MessageBus.INSTANCE.SendError(new(AppIcons.DOCUMENT, string.Format(TB("Could not open the document: {0}"), issue)));
        }

        /// <summary>
        /// Tells the user on which page the passage is, when the document opened without that page, and
        /// why AI Studio could not open it there.
        /// </summary>
        /// <remarks>
        /// Each reason lies outside of AI Studio, and the notice says so: the sandbox of the Flatpak, a
        /// program which does not let anybody choose its page, or a program which did not start with
        /// one. A reason of a newer runtime is told like a program which takes no page.
        /// </remarks>
        /// <param name="response">How opening the document went.</param>
        /// <returns>The notice, or null when the page arrived, none was asked for, or the document did not open at all, which the user hears about as an error.</returns>
        internal string? GetPageNotice(OpenDocumentResponse response)
        {
            if (!response.Success || document.PageNumber is not { } page || page <= 0)
                return null;

            return response.PageOutcome switch
            {
                DocumentPageOutcome.APPLIED or DocumentPageOutcome.NOT_REQUESTED or DocumentPageOutcome.DOCUMENT_NOT_OPENED => null,
                DocumentPageOutcome.SANDBOXED => string.Format(TB("As a Flatpak, AI Studio cannot open the document at its page: the sandbox of the Flatpak lets no page through to the program which shows the document. The passage is on page {0}."), page),
                DocumentPageOutcome.PROGRAM_FAILED => string.Format(TB("AI Studio could not open the document at its page: the program which shows the document could not be started with the page, so it opened without it. The passage is on page {0}."), page),
                _ => string.Format(TA("AI Studio cannot open the document at its page: the program your system shows it with does not allow choosing a page. The passage is on page {0}."), page),
            };
        }

        /// <summary>
        /// Opens the file browser of the system and selects the document in it.
        /// </summary>
        /// <remarks>
        /// The second way out: a document which the system opens in the wrong program, or which the user
        /// wants to move or send on instead of read, is reached from here without being opened. This is
        /// the same way out the embeddings page offers for a file it could not read.
        /// </remarks>
        /// <param name="rustService">The runtime, which opens the file browser.</param>
        public async Task ShowInFileManagerAsync(RustService rustService)
        {
            OpenPathResponse response;
            try
            {
                response = await rustService.TryOpenPathInRuntimeFileManager(document.Path);
            }
            catch (Exception e)
            {
                LOGGER.LogWarning(e, "Could not show a source document in the file manager.");
                await MessageBus.INSTANCE.SendError(new(AppIcons.FOLDER_OPEN, TB("Could not open the file location.")));
                return;
            }

            if (response.Success)
                return;

            var issue = string.IsNullOrWhiteSpace(response.Issue) ? TB("Unknown error") : response.Issue;
            await MessageBus.INSTANCE.SendError(new(AppIcons.FOLDER_OPEN, string.Format(TB("Could not open the file location: {0}"), issue)));
        }
    }
}