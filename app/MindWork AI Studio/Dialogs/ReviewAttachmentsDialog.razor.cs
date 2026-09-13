using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class ReviewAttachmentsDialog : MSGComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ReviewAttachmentsDialog).Namespace, nameof(ReviewAttachmentsDialog));

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public HashSet<FileAttachment> DocumentPaths { get; set; } = new();

    /// <summary>
    /// Attaches the files the user drops onto this dialog, and answers which of them it attached.
    /// </summary>
    /// <remarks>
    /// Null when this dialog only shows attachments, which is the case for a message that was
    /// already sent: there is nothing left to attach to. Without this, the dialog behaves as it
    /// always did and swallows every drop.
    /// </remarks>
    [Parameter]
    public Func<List<string>, Task<IReadOnlyList<FileAttachment>>>? AttachPaths { get; set; }

    /// <summary>
    /// Decides, at the moment a drop arrives, whether attaching is possible right now.
    /// </summary>
    /// <remarks>
    /// Asked rather than passed as a value, because the answer changes while this dialog is open:
    /// dropping a media file here starts a transcription, and nothing else may be attached until
    /// that one is through.
    /// </remarks>
    [Parameter]
    public Func<bool>? IsAttachingUnavailable { get; set; }

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    private void Close() => this.MudDialog.Close(DialogResult.Ok(this.DocumentPaths));

    /// <summary>Whether this dialog takes files at all, which decides what it says and shows.</summary>
    private bool CanAttach => this.AttachPaths is not null;

    private bool IsZoneDisabled() => this.IsAttachingUnavailable?.Invoke() ?? false;

    /// <summary>
    /// Binds the drop zone only when there is something to attach to. An area which reports a
    /// delegate claims the role of its own default target, and claiming it without being able to
    /// use it would swallow drops with no reason the user could see.
    /// </summary>
    private EventCallback<List<string>> DropCallback => this.AttachPaths is null
        ? default
        : EventCallback.Factory.Create<List<string>>(this, this.PathsDropped);

    /// <summary>
    /// Marks the list of attachments while a file hovers over this dialog, so it is visible where
    /// the file would land. The frame keeps its width in both states; only its color changes, or
    /// the list would jump by a few pixels with every drag.
    /// </summary>
    /// <param name="isDropTarget">Whether this dialog is the target of the drop being aimed right now.</param>
    private string AttachmentListClass(bool isDropTarget)
    {
        if (!this.CanAttach)
            return "pa-2";

        return isDropTarget && !this.IsZoneDisabled()
            ? "border-dashed border-2 rounded-lg pa-2 mud-border-primary"
            : "border-dashed border-2 rounded-lg pa-2 mud-border-lines-default";
    }

    /// <summary>
    /// The attachments, sorted by their folder and, within it, by their file name.
    /// </summary>
    /// <remarks>
    /// The list below starts a new heading whenever the folder changes from one attachment to the
    /// next, which names every folder exactly once -- but only as long as the attachments of a
    /// folder arrive together. The set behind them keeps no order of its own to guarantee that:
    /// removing one attachment already scrambles it, and one attached while this dialog is open
    /// lands at its end, giving its folder a second heading further down. Sorting here is what that
    /// list assumes anyway.
    /// </remarks>
    private IEnumerable<FileAttachment> OrderedAttachments => this.DocumentPaths
        .OrderBy(attachment => Path.GetDirectoryName(attachment.FilePath) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        .ThenBy(attachment => attachment.FileName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Attaches what the user dropped onto this dialog and answers which files that became.
    /// </summary>
    /// <remarks>
    /// Every drop takes this way, the ones aimed at the document preview above this dialog
    /// included. That is why the list is refreshed here and nowhere else.
    /// </remarks>
    /// <param name="paths">The dropped paths, in the order the runtime delivered them.</param>
    /// <returns>The files which were attached, in the order they were dropped.</returns>
    private async Task<IReadOnlyList<FileAttachment>> AttachPathsAsync(List<string> paths)
    {
        if (this.AttachPaths is null)
            return [];

        var attached = await this.AttachPaths(paths);
        this.StateHasChanged();

        //
        // The list scrolls, so a newly attached file may well sit outside the visible part of it.
        // Saying so is cheaper than scrolling there, and the snackbar is skipped by the hit test,
        // so it never gets in the way of the next drop.
        //
        if (attached.Count > 0)
            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.AttachFile, attached.Count is 1
                ? string.Format(T("Attached {0}."), attached[0].FileName)
                : string.Format(T("Attached {0} files."), attached.Count)));

        return attached;
    }

    private async Task PathsDropped(List<string> paths) => await this.AttachPathsAsync(paths);

    public static async Task<HashSet<FileAttachment>> OpenDialogAsync(IDialogService dialogService, HashSet<FileAttachment> documentPaths, Func<List<string>, Task<IReadOnlyList<FileAttachment>>>? attachPaths = null, Func<bool>? isAttachingUnavailable = null)
    {
        var dialogParameters = new DialogParameters<ReviewAttachmentsDialog>
        {
            { x => x.DocumentPaths, documentPaths }
        };

        if (attachPaths is not null)
            dialogParameters.Add(x => x.AttachPaths, attachPaths);

        if (isAttachingUnavailable is not null)
            dialogParameters.Add(x => x.IsAttachingUnavailable, isAttachingUnavailable);

        var dialogReference = await dialogService.ShowAsync<ReviewAttachmentsDialog>(TB("Your attached files"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return documentPaths;

        if (dialogResult.Data is null)
            return documentPaths;

        return dialogResult.Data as HashSet<FileAttachment> ?? documentPaths;
    }

    private void DeleteAttachment(FileAttachment fileAttachment)
    {
        if (this.DocumentPaths.Remove(fileAttachment))
        {
            this.StateHasChanged();
        }
    }
    
    /// <summary>
    /// The user might want to check what we actually extract from his file and therefore give the LLM as an input.
    /// </summary>
    /// <param name="fileAttachment">The file to check.</param>
    private async Task InvestigateFile(FileAttachment fileAttachment)
    {
        var dialogParameters = new DialogParameters<DocumentCheckDialog>
        {
            { x => x.Document, fileAttachment },
        };

        await this.DialogService.ShowAsync<DocumentCheckDialog>(T("Document Preview"), dialogParameters, DialogOptions.FULLSCREEN);
    }
}