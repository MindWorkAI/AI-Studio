using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Tools.Security;
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
    /// Set when reading the file failed, so the dialog shows the reason instead of empty content.
    /// </summary>
    private string? loadFailureMessage;

    /// <summary>
    /// True while we extract the file content. Reading happens after the first render, so the
    /// dialog can tell the user that it is working instead of showing an empty document.
    /// </summary>
    private bool isLoadingContent;

    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ILogger<DocumentCheckDialog> Logger { get; init; } = null!;
    
    protected override async Task OnInitializedAsync()
    {
        //
        // Decide before the first render whether we have to read the file at all. Images are shown
        // as they are, a missing file shows its own message, and content a caller already handed
        // us is reused instead of being extracted a second time:
        //
        this.isLoadingContent =
            this.Document is not null &&
            !this.Document.IsImage &&
            this.Document.Exists &&
            string.IsNullOrWhiteSpace(this.FileContent);

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && this.Document is not null)
        {
            if (!this.isLoadingContent)
                return;

            try
            {
                var extraction = await UserFile.LoadFileData(this.Document.FilePath, this.RustService, this.DialogService);
                this.FileContent = extraction.Content;

                //
                // This dialog exists so the user can check what we hand to the AI. Showing an
                // empty document when reading the file failed would answer that question wrong.
                //
                if (!extraction.HasUsableContent)
                    this.loadFailureMessage = extraction.ToUserMessage(this.Document.FileName);
            }
            catch (PromptInjectionBlockedException exception)
            {
                this.Logger.LogWarning(exception, "Blocked suspected prompt injection while previewing '{FilePath}'", this.Document?.FilePath);
                this.FileContent = string.Empty;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to load file content from '{FilePath}'", this.Document);
                this.FileContent = string.Empty;
                this.loadFailureMessage = FileExtractionErrorCode.INTERNAL.ToUserMessage(this.Document.FileName);
            }
            finally
            {
                this.isLoadingContent = false;
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