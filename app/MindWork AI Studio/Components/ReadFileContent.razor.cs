using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

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

    [Inject]
    private PandocAvailabilityService PandocAvailabilityService { get; init; } = null!;
    
    private async Task SelectFile()
    {
        // Ensure that Pandoc is installed and ready:
        var pandocState = await this.PandocAvailabilityService.EnsureAvailabilityAsync(
            showSuccessMessage: false,
            showDialog: true);

        // Check if Pandoc is available after the check / installation:
        if (!pandocState.IsAvailable)
        {
            this.Logger.LogWarning("The user cancelled the Pandoc installation or Pandoc is not available. Aborting file selection.");
            return;
        }

        var selectedFile = await this.RustService.SelectFile(T("Select file to read its content"));
        if (selectedFile.UserCancelled)
        {
            this.Logger.LogInformation("User cancelled the file selection");
            return;
        }

        if(!File.Exists(selectedFile.SelectedFilePath))
        {
            this.Logger.LogWarning("Selected file does not exist: '{FilePath}'", selectedFile.SelectedFilePath);
            return;
        }

        if (!await FileExtensionValidation.IsExtensionValidWithNotifyAsync(selectedFile.SelectedFilePath))
        {
            this.Logger.LogWarning("User attempted to load unsupported file: {FilePath}", selectedFile.SelectedFilePath);
            return;
        }

        try
        {
            var fileContent = await UserFile.LoadFileData(selectedFile.SelectedFilePath, this.RustService, this.DialogService);
            await this.FileContentChanged.InvokeAsync(fileContent);
            this.Logger.LogInformation("Successfully loaded file content: {FilePath}", selectedFile.SelectedFilePath);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to load file content: {FilePath}", selectedFile.SelectedFilePath);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Error, T("Failed to load file content")));
        }
    }
}