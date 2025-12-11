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
    public string FilePath { get; set; } = string.Empty;
    
    private void Close() => this.MudDialog.Cancel();
    
    [Parameter]
    public string FileContent { get; set; } = string.Empty;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ILogger<ReadFileContent> Logger { get; init; } = null!;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !string.IsNullOrWhiteSpace(this.FilePath))
        {
            try
            {
                var fileContent = await UserFile.LoadFileData(this.FilePath, this.RustService, this.DialogService);
                this.FileContent = fileContent;
                this.StateHasChanged();
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to load file content from '{FilePath}'", this.FilePath);
                this.FileContent = string.Empty;
                this.StateHasChanged();
            }
        }
        else if (firstRender)
            this.Logger.LogWarning("Document check dialog opened without a valid file path");
    }
    
    private CodeBlockTheme CodeColorPalette => this.SettingsManager.IsDarkMode ? CodeBlockTheme.Dark : CodeBlockTheme.Default;

    private MudMarkdownStyling MarkdownStyling => new()
    {
        CodeBlock = { Theme = this.CodeColorPalette },
    };
}