using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ReadFileContent : MSGComponentBase
{
    [Parameter]
    public string FileContent { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> FileContentChanged { get; set; }
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
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
        
        var fileContent = await this.RustService.ReadArbitraryFileData(selectedFile.SelectedFilePath, int.MaxValue);
        await this.FileContentChanged.InvokeAsync(fileContent);
    }
}