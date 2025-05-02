using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ReadPDFContent : MSGComponentBase
{
    [Parameter]
    public string PDFContent { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> PDFContentChanged { get; set; }
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    private async Task SelectFile()
    {
        var pdfFile = await this.RustService.SelectFile(T("Select PDF file"));
        if (pdfFile.UserCancelled)
            return;

        if (!pdfFile.SelectedFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return;
        
        if(!File.Exists(pdfFile.SelectedFilePath))
            return;
        
        var pdfText = await this.RustService.GetPDFText(pdfFile.SelectedFilePath);
        await this.PDFContentChanged.InvokeAsync(pdfText);
    }
}