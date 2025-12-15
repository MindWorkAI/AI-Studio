using AIStudio.Components;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class ReviewAttachmentsDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public HashSet<string> DocumentPaths { get; set; } = new();
    
    [Inject]
    private IDialogService DialogService { get; set; } = null!;
    
    private void Close() => this.MudDialog.Close(DialogResult.Ok(this.DocumentPaths));
    
    public static async Task<HashSet<string>> OpenDialogAsync(IDialogService dialogService, params HashSet<string> documentPaths)
    {
        var dialogParameters = new DialogParameters<ReviewAttachmentsDialog>
        {
            { x => x.DocumentPaths, documentPaths } 
        };
        
        var dialogReference = await dialogService.ShowAsync<ReviewAttachmentsDialog>("Your attached documents", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return documentPaths;

        if (dialogResult.Data is null)
            return documentPaths;
        
        return dialogResult.Data as HashSet<string> ?? documentPaths;
    }
}