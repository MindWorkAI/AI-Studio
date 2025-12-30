using AIStudio.Chat;
using AIStudio.Components;
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
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ILogger<DocumentCheckDialog> Logger { get; init; } = null!;
    
    private string imageDataUrl = string.Empty;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && this.Document is not null)
        {
            try
            {
                if (this.Document.IsImage)
                {
                    // Load image as Base64 data URL since browsers cannot access local file:// URLs:
                    if (this.Document is FileAttachmentImage imageAttachment)
                    {
                        var (success, base64Content) = await imageAttachment.TryAsBase64();
                        if (success)
                        {
                            var mimeType = imageAttachment.DetermineMimeType();
                            this.imageDataUrl = ImageHelpers.ToDataUrl(base64Content, mimeType);
                            this.StateHasChanged();
                        }
                    }
                }
                else
                {
                    var fileContent = await UserFile.LoadFileData(this.Document.FilePath, this.RustService, this.DialogService);
                    this.FileContent = fileContent;
                    this.StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to load file content from '{FilePath}'", this.Document);
                this.FileContent = string.Empty;
                this.StateHasChanged();
            }
        }
        else if (firstRender)
            this.Logger.LogWarning("Document check dialog opened without a valid file path.");
    }
    
    private CodeBlockTheme CodeColorPalette => this.SettingsManager.IsDarkMode ? CodeBlockTheme.Dark : CodeBlockTheme.Default;

    private MudMarkdownStyling MarkdownStyling => new()
    {
        CodeBlock = { Theme = this.CodeColorPalette },
    };
}