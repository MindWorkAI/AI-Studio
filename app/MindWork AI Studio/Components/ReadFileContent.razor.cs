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
        var txtFile = await this.RustService.SelectFile("Select Text file");
        if (txtFile.UserCancelled)
            return;
        
        if(!File.Exists(txtFile.SelectedFilePath))
            return;
        
        var txtContent = await this.RustService.ReadArbitraryFileData(txtFile.SelectedFilePath, int.MaxValue);
        
        await this.FileContentChanged.InvokeAsync(txtContent);
    }
}