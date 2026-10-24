using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Check how your file will be loaded.
/// </summary>
public partial class DocumentCheckDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public FileAttachment? Document { get; set; }
    
    private void Close() => this.MudDialog.Cancel();
    
    [Parameter]
    public string FileContent { get; set; } = string.Empty;

    /// <summary>
    /// Attaches the files the user drops onto this dialog, and answers which of them it attached.
    /// </summary>
    /// <remarks>
    /// Null when our caller has no list of attachments to add to, which is the case for the prompt
    /// guide preview of the Prompt Optimizer. This dialog then shows its document and nothing else,
    /// exactly as it always did.
    /// </remarks>
    [Parameter]
    public Func<List<string>, Task<IReadOnlyList<FileAttachment>>>? AttachPaths { get; set; }

    /// <summary>
    /// Decides, at the moment a drop arrives, whether attaching is possible right now.
    /// </summary>
    /// <remarks>
    /// Asked rather than passed as a value, because the answer changes while this dialog is open:
    /// dropping a media file starts a transcription, and nothing else may be attached until that
    /// one is through.
    /// </remarks>
    [Parameter]
    public Func<bool>? IsAttachingUnavailable { get; set; }

    /// <summary>
    /// Keeps what this dialog read of a document which was never sent, so that sending it does not
    /// read the file again, see FileAttachment.ProvenanceId.
    /// </summary>
    /// <remarks>
    /// Answers the attachment with the entry of its text, or null when nothing was kept. Only the
    /// chat hands one over, for the attachments of the message being written: a sent message has
    /// its texts already, and elsewhere, nothing is sent later.
    /// </remarks>
    [Parameter]
    public Func<FileAttachment, string, Task<FileAttachment?>>? KeepReadText { get; set; }

    /// <summary>
    /// The document we show right now. It starts out as the one we were opened with and changes
    /// whenever the user drops another file onto this dialog.
    /// </summary>
    /// <remarks>
    /// Kept in a field rather than read from the parameter: the dialog fragment is rendered again
    /// with the parameters captured when it was opened, whenever something about the dialog stack
    /// changes. That happens in the middle of a drop, because attaching may open the Pandoc dialog
    /// or ask the user about a media file -- reading the parameter would undo the switch right
    /// after it was made.
    /// </remarks>
    private FileAttachment? document;

    /// <summary>
    /// The content of the document we show, either handed to us by our caller or read by us.
    /// </summary>
    private string fileContent = string.Empty;

    /// <summary>
    /// How many characters we show at most. Rendering a huge document costs us a large Markdown
    /// syntax tree and an equally large render tree. This dialog answers the question of how we
    /// read the file, though — the beginning of the document is enough for that, and the AI still
    /// receives the entire content.
    /// </summary>
    private const int PREVIEW_CHARACTER_LIMIT = 200_000;

    /// <summary>
    /// Set when reading the file failed, so the dialog shows the reason instead of empty content.
    /// </summary>
    private string? loadFailureMessage;

    /// <summary>
    /// Ends the extraction when this dialog is gone, or when another document took the place of
    /// the one being read, before that file was read completely.
    /// </summary>
    private CancellationTokenSource extractionCancellation = new();

    /// <summary>
    /// Numbers the loads, so that a load can tell whether it still owns this dialog.
    /// </summary>
    /// <remarks>
    /// Cancelling ends the waiting, not the code behind it: what follows every await of an
    /// abandoned load runs regardless. Without this number, its final block would clear the loading
    /// state of the load which replaced it, and the new document would never leave its skeletons.
    /// </remarks>
    private int loadGeneration;

    /// <summary>
    /// True once this dialog was disposed. The extraction runs across awaits, so it may return
    /// long after the user closed the dialog — it must not touch this component afterwards.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// True while we extract the file content. Reading happens after the first render, so the
    /// dialog can tell the user that it is working instead of showing an empty document.
    /// </summary>
    private bool isLoadingContent;

    /// <summary>
    /// True when the content is the text the AI received from the document when it was sent, rather
    /// than what the file holds now, see FileAttachment.ProvenanceId.
    /// </summary>
    private bool showsStoredText;

    /// <summary>
    /// Whether the document we show is no longer at its place.
    /// </summary>
    private bool IsFileMissing => this.document is not null && !this.document.Exists;

    [Inject]
    private DatabaseClientProvider DatabaseClientProvider { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ILogger<DocumentCheckDialog> Logger { get; init; } = null!;

    [Inject]
    private PandocAvailabilityService PandocAvailability { get; init; } = null!;
    
    protected override async Task OnInitializedAsync()
    {
        this.document = this.Document;
        this.fileContent = this.FileContent;

        this.isLoadingContent = this.NeedsExtraction();
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        if (this.document is null)
        {
            this.Logger.LogWarning("Document check dialog opened without a valid file path.");
            return;
        }

        await this.LoadDocumentContentAsync();
    }

    /// <summary>
    /// Whether the document we show has to be read before we can show anything of it. Images are
    /// shown as they are, a missing file shows its own message unless the text the AI received from
    /// it is stored, and content a caller already handed us is reused instead of being extracted a
    /// second time.
    /// </summary>
    private bool NeedsExtraction() =>
        this.document is not null &&
        !this.document.IsImage &&
        (this.document.ProvenanceId is not null || this.document.Exists) &&
        string.IsNullOrWhiteSpace(this.fileContent);

    /// <summary>
    /// Reads the content of the document we show and puts it into the preview.
    /// </summary>
    /// <remarks>
    /// Runs after a render, so the user sees that we are working instead of an empty document. It
    /// is called for the document this dialog was opened with, and again for every file the user
    /// drops onto it.
    /// </remarks>
    private async Task LoadDocumentContentAsync()
    {
        if (this.document is null || !this.isLoadingContent)
            return;

        //
        // A drop may arrive while we are still reading the file before it. We number this load and
        // end the previous one, so that what is left of it recognizes that this dialog has moved on:
        //
        var generation = ++this.loadGeneration;
        var documentToLoad = this.document;

        var previousCancellation = this.extractionCancellation;
        this.extractionCancellation = new();
        var cancellationToken = this.extractionCancellation.Token;

        await previousCancellation.CancelAsync();
        previousCancellation.Dispose();

        if (this.isDisposed || generation != this.loadGeneration)
            return;

        try
        {
            //
            // A document sent before shows the text the AI received from it, which the provenance
            // store keeps, also when the file changed or is gone by now. Without it, we read the file:
            //
            if (documentToLoad.ProvenanceId is { } provenanceId)
            {
                var storedText = await this.ReadStoredTextAsync(provenanceId);
                if (this.isDisposed || generation != this.loadGeneration)
                    return;

                if (storedText is not null)
                {
                    this.fileContent = storedText;
                    this.showsStoredText = true;
                    return;
                }

                // Nothing left to read, which the dialog says:
                if (!documentToLoad.Exists)
                    return;
            }

            var extraction = await UserFile.LoadFileData(documentToLoad.FilePath, this.RustService, this.PandocAvailability, cancellationToken);
            if (this.isDisposed || generation != this.loadGeneration)
                return;

            this.fileContent = extraction.Content;

            //
            // This dialog exists so the user can check what we hand to the AI. Showing an
            // empty document when reading the file failed would answer that question wrong.
            //
            if (!extraction.HasUsableContent)
                this.loadFailureMessage = extraction.ToUserMessage(documentToLoad.FileName);
            else if (this.KeepReadText is not null && documentToLoad.ProvenanceId is null)
                await this.KeepReadTextAsync(documentToLoad, extraction, generation);
        }
        catch (OperationCanceledException)
        {
            // Either the user closed this dialog, or another document took the place of this one
            // while we were reading it. Nothing left to do in both cases.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to load file content from '{FilePath}'", documentToLoad.FilePath);
            if (this.isDisposed || generation != this.loadGeneration)
                return;

            this.fileContent = string.Empty;
            this.loadFailureMessage = FileExtractionErrorCode.INTERNAL.ToUserMessage(documentToLoad.FileName);
        }
        finally
        {
            if (!this.isDisposed && generation == this.loadGeneration)
            {
                this.isLoadingContent = false;
                this.StateHasChanged();
            }
        }
    }

    /// <summary>
    /// Whether a dropped file can be both attached and shown here, which decides what this dialog
    /// says and shows -- and whether it takes drops at all.
    /// </summary>
    /// <remarks>
    /// Without a document, this dialog offers a file to be loaded instead, and that field is the
    /// default target of this dialog. An area which reports a delegate claims that role for itself
    /// and would take every drop away from the field, so we stay a plain marker in that case.
    /// </remarks>
    private bool CanAttach => this.AttachPaths is not null && this.document is not null;

    private EventCallback<List<string>> DropCallback => this.CanAttach
        ? EventCallback.Factory.Create<List<string>>(this, this.PathsDropped)
        : default;

    private bool IsZoneDisabled() => this.IsAttachingUnavailable?.Invoke() ?? false;

    /// <summary>
    /// Marks the part of this dialog which shows the document while a file hovers over it, so it is
    /// visible where that file would land. The frame keeps its width in both states; only its color
    /// changes, or the content would jump by a few pixels with every drag.
    /// </summary>
    /// <param name="isDropTarget">Whether this dialog is the target of the drop being aimed right now.</param>
    private string PreviewAreaClass(bool isDropTarget)
    {
        if (!this.CanAttach)
            return string.Empty;

        return isDropTarget && !this.IsZoneDisabled()
            ? "border-dashed border-2 rounded-lg pa-2 mud-border-primary"
            : "border-dashed border-2 rounded-lg pa-2 mud-border-lines-default";
    }

    /// <summary>
    /// Attaches what the user dropped onto this dialog and shows the first file of it.
    /// </summary>
    /// <param name="paths">The dropped paths, in the order the runtime delivered them.</param>
    private async Task PathsDropped(List<string> paths)
    {
        if (this.AttachPaths is null)
            return;

        var attached = await this.AttachPaths(paths);
        if (this.isDisposed)
            return;

        //
        // Nothing came of the drop: the file is of a kind we do not take, Pandoc is missing, the
        // validation refused it, or it is a media file whose transcript does not exist yet. The
        // reason is already on its way to the user, and the document they were looking at stays.
        //
        if (attached.Count is 0)
            return;

        this.ShowDocument(attached[0]);

        //
        // Render before reading: the skeletons of the loading state are what tells the user that
        // the preview switched at all, and reading a file may well take a moment.
        //
        this.StateHasChanged();
        await this.LoadDocumentContentAsync();
    }

    /// <summary>
    /// Shows another document, discarding everything that belonged to the previous one.
    /// </summary>
    /// <param name="attachment">The document to show from now on.</param>
    private void ShowDocument(FileAttachment attachment)
    {
        this.document = attachment;
        this.fileContent = string.Empty;
        this.loadFailureMessage = null;
        this.showsStoredText = false;
        this.isLoadingContent = this.NeedsExtraction();
    }

    /// <summary>
    /// Keeps what this dialog read of a document, so that sending it does not read the file again.
    /// </summary>
    /// <param name="documentToKeep">The document, as the caller holds it.</param>
    /// <param name="extraction">What reading it gave.</param>
    /// <param name="generation">The number of the load, which tells whether this dialog still shows the document.</param>
    private async Task KeepReadTextAsync(FileAttachment documentToKeep, FileExtractionResult extraction, int generation)
    {
        // Sending reads the file no more, so it would no longer tell what was missing or mislabeled:
        await ContentText.ReportIncompleteReadingAsync(documentToKeep, extraction);

        var kept = await this.KeepReadText!(documentToKeep, extraction.Content);
        if (kept is null || this.isDisposed || generation != this.loadGeneration)
            return;

        // A second preview of the same attachment then shows the kept text without reading the file:
        this.document = kept;
    }

    /// <summary>
    /// Reads the text the AI received from a document when it was sent.
    /// </summary>
    /// <param name="provenanceId">The entry of the text.</param>
    /// <returns>The text, or null when the provenance store no longer holds it, or cannot be read.</returns>
    private async Task<string?> ReadStoredTextAsync(Guid provenanceId)
    {
        try
        {
            var store = await this.DatabaseClientProvider.GetProvenanceStoreAsync();
            var entries = await store.GetEntriesAsync([provenanceId], CancellationToken.None);
            return entries.GetValueOrDefault(provenanceId)?.Content;
        }
        catch (Exception exception)
        {
            this.Logger.LogWarning(exception, "Failed to read the stored text of the document '{ProvenanceId}'. Its file is read instead.", provenanceId);
            return null;
        }
    }

    /// <summary>
    /// An image is shown as it is; everything else is shown as the text we read from it.
    /// </summary>
    private ContentViewerKind ViewerKind => (this.document?.IsImage ?? false) ? ContentViewerKind.IMAGE : ContentViewerKind.TEXT;

    /// <summary>
    /// Opens the document we show in the program the system uses for it, at its beginning: the
    /// preview shows how we read the file, not a place in it.
    /// </summary>
    private async Task OpenDocument()
    {
        if (this.document is null)
            return;

        await new SourceDocumentLocation(this.document.FilePath, null).OpenAsync(this.RustService);
    }

    /// <summary>
    /// Explains why we show only the beginning of a large document. The preview only answers how we
    /// read the file, so it may leave out the rest; the AI does not.
    /// </summary>
    /// <param name="cutOffCharacters">How many characters the preview leaves out.</param>
    private string CutOffNotice(int cutOffCharacters) => string.Format(T("Your document is large, so we show you only its beginning. We hide the remaining {0:N0} characters here. Rest assured: the AI still receives your entire document."), cutOffCharacters);

    /// <summary>
    /// Ends a running extraction. Without this, reading a large document would continue after the
    /// user closed this dialog and would keep this component, the extracted content, and the
    /// response stream alive until the runtime is done.
    /// </summary>
    protected override void DisposeResources()
    {
        this.isDisposed = true;

        //
        // Only the running load is left to end here: every load we replaced was ended and disposed
        // the moment its successor started.
        //
        this.extractionCancellation.Cancel();
        this.extractionCancellation.Dispose();

        base.DisposeResources();
    }
}