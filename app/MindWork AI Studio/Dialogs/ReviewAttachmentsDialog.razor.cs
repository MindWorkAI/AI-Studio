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

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    private void Close() => this.MudDialog.Close(DialogResult.Ok(this.DocumentPaths));

    public static async Task<HashSet<FileAttachment>> OpenDialogAsync(IDialogService dialogService, params HashSet<FileAttachment> documentPaths)
    {
        var dialogParameters = new DialogParameters<ReviewAttachmentsDialog>
        {
            { x => x.DocumentPaths, documentPaths }
        };

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
}