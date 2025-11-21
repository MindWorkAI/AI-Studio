using AIStudio.Dialogs;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class ReadFileContent : MSGComponentBase
{
    [Parameter]
    public string Text { get; set; } = string.Empty;
    
    [Parameter]
    public string FileContent { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> FileContentChanged { get; set; }
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ILogger<ReadFileContent> Logger { get; init; } = null!;
    
    private async Task SelectFile()
    {
        var selectedFile = await this.RustService.SelectFile(T("Select file to read its content"));
        if (selectedFile.UserCancelled)
            return;
        
        if(!File.Exists(selectedFile.SelectedFilePath))
            return;
        
        var ext = Path.GetExtension(selectedFile.SelectedFilePath).TrimStart('.');
        if (Array.Exists(FileTypeFilter.Executables.FilterExtensions, x => x.Equals(ext,  StringComparison.OrdinalIgnoreCase)))
        {
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.AppBlocking, T("Executables are not allowed")));
            return;
        }
        
        if (Array.Exists(FileTypeFilter.AllImages.FilterExtensions, x => x.Equals(ext,  StringComparison.OrdinalIgnoreCase)))
        {
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.ImageNotSupported, T("Images are not supported yet")));
            return;
        }
        
        if (Array.Exists(FileTypeFilter.AllVideos.FilterExtensions, x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.FeaturedVideo, this.T("Videos are not supported yet")));
            return;
        }
        
        // Ensure that Pandoc is installed and ready:
        var pandocState = await Pandoc.CheckAvailabilityAsync(this.RustService, showSuccessMessage: false);
        if (!pandocState.IsAvailable)
        {
            var dialogParameters = new DialogParameters<PandocDialog>
            {
                { x => x.ShowInitialResultInSnackbar, false },
            };
                
            var dialogReference = await this.DialogService.ShowAsync<PandocDialog>(T("Pandoc Installation"), dialogParameters, DialogOptions.FULLSCREEN);
            await dialogReference.Result;
                
            pandocState = await Pandoc.CheckAvailabilityAsync(this.RustService, showSuccessMessage: true);
            if (!pandocState.IsAvailable)
            {
                this.Logger.LogError("Pandoc is not available after installation attempt.");
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, T("Pandoc may be required for importing files.")));
            }
        }
        
        var fileContent = await this.RustService.ReadArbitraryFileData(selectedFile.SelectedFilePath, int.MaxValue);
        await this.FileContentChanged.InvokeAsync(fileContent);
    }
}