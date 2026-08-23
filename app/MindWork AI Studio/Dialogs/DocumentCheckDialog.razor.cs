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

    /// <summary>
    /// How many characters we show at most. Rendering a huge document costs us a large Markdown
    /// syntax tree and an equally large render tree. This dialog answers the question of how we
    /// read the file, though — the beginning of the document is enough for that, and the AI still
    /// receives the entire content.
    /// </summary>
    private const int PREVIEW_CHARACTER_LIMIT = 200_000;

    /// <summary>
    /// Set when reading the file failed, so the dialog shows the reason instead of empty content.
    /// </summary>
    private string? loadFailureMessage;

    /// <summary>
    /// What we show to the user: either the entire file content, or its beginning. We keep this in
    /// its own field so that we cut the content only once, instead of on every render.
    /// </summary>
    private string previewContent = string.Empty;

    /// <summary>
    /// How many characters we cut off from the preview. Zero when we show the entire content.
    /// </summary>
    private int previewCutOffCharacters;

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

        this.UpdatePreview();
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
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to load file content from '{FilePath}'", this.Document);
                this.FileContent = string.Empty;
                this.loadFailureMessage = FileExtractionErrorCode.INTERNAL.ToUserMessage(this.Document.FileName);
            }
            finally
            {
                this.isLoadingContent = false;
                this.UpdatePreview();
                this.StateHasChanged();
            }
        }
        else if (firstRender)
            this.Logger.LogWarning("Document check dialog opened without a valid file path.");
    }
    
    /// <summary>
    /// Called when the user loads a file through this dialog. We don't use a two-way binding here,
    /// since we have to refresh the preview whenever the content changes.
    /// </summary>
    /// <param name="fileContent">The content of the file the user has loaded.</param>
    private void ApplyLoadedFileContent(string fileContent)
    {
        this.FileContent = fileContent;
        this.UpdatePreview();
    }

    /// <summary>
    /// Determines what part of the file content we show to the user.
    /// </summary>
    private void UpdatePreview()
    {
        if (this.FileContent.Length <= PREVIEW_CHARACTER_LIMIT)
        {
            this.previewContent = this.FileContent;
            this.previewCutOffCharacters = 0;
            return;
        }

        //
        // We cut at the last line break before our limit. Otherwise, we might tear apart a Markdown
        // construct like a table row or a code fence in the middle of a line:
        //
        var cutIndex = this.FileContent.LastIndexOf('\n', PREVIEW_CHARACTER_LIMIT - 1) + 1;
        if (cutIndex < 1)
            cutIndex = PREVIEW_CHARACTER_LIMIT;

        this.previewContent = this.FileContent[..cutIndex];
        this.previewCutOffCharacters = this.FileContent.Length - cutIndex;
    }

    private CodeBlockTheme CodeColorPalette => this.SettingsManager.IsDarkMode ? CodeBlockTheme.Dark : CodeBlockTheme.Default;

    private MudMarkdownStyling MarkdownStyling => new()
    {
        CodeBlock = { Theme = this.CodeColorPalette },
    };
}