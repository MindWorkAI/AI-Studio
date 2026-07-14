using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;
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

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool EnableDragDrop { get; set; }

    /// <summary>
    /// On which layer to register the drop area. Higher layers have priority over lower layers.
    /// </summary>
    [Parameter]
    public int Layer { get; set; }

    /// <summary>
    /// Catch all documents that are hovered over the AI Studio window and not only over the drop zone.
    /// </summary>
    [Parameter]
    public bool CatchAllDocuments { get; set; }
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ILogger<ReadFileContent> Logger { get; init; } = null!;

    [Inject]
    private PandocAvailabilityService PandocAvailabilityService { get; init; } = null!;

    private const string DEFAULT_DRAG_CLASS = "relative rounded-lg border-2 border-dashed pa-3 mb-3 mud-width-full";

    private string ButtonText => string.IsNullOrWhiteSpace(this.Text) ? T("Use file content as input") : this.Text;
    private string dragClass = DEFAULT_DRAG_CLASS;
    private uint numDropAreasAboveThis;
    private bool isComponentHovered;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        if (this.EnableDragDrop)
        {
            this.ApplyFilters([], [ Event.TAURI_EVENT_RECEIVED, Event.REGISTER_FILE_DROP_AREA, Event.UNREGISTER_FILE_DROP_AREA ]);
            await this.MessageBus.SendMessage(this, Event.REGISTER_FILE_DROP_AREA, this.Layer);
        }

        await base.OnInitializedAsync();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (!this.EnableDragDrop)
            return;

        if (this.Disabled && triggeredEvent == Event.TAURI_EVENT_RECEIVED)
            return;

        switch (triggeredEvent)
        {
            case Event.REGISTER_FILE_DROP_AREA when sendingComponent != this:
            {
                if(data is int layer && layer > this.Layer)
                {
                    this.numDropAreasAboveThis++;
                    this.ClearDragClass();
                }

                break;
            }

            case Event.UNREGISTER_FILE_DROP_AREA when sendingComponent != this:
            {
                if(data is int layer && layer > this.Layer && this.numDropAreasAboveThis > 0)
                    this.numDropAreasAboveThis--;

                break;
            }

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_HOVERED }:
                if(!this.CanCatchDroppedFile())
                    return;

                this.SetDragClass();
                this.StateHasChanged();
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_CANCELED }:
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.WINDOW_NOT_FOCUSED }:
                this.isComponentHovered = false;
                this.ClearDragClass();
                this.StateHasChanged();
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_DROPPED, Payload: var paths }:
                if(!this.CanCatchDroppedFile())
                    return;

                await this.LoadFirstValidFile(paths);
                this.ClearDragClass();
                this.StateHasChanged();
                break;
        }
    }

    #endregion
    
    private async Task SelectFile()
    {
        if (this.Disabled)
            return;

        if (!await this.EnsurePandocAvailability())
            return;

        var selectedFile = await this.RustService.SelectFile(T("Select file to read its content"));
        if (selectedFile.UserCancelled)
        {
            this.Logger.LogInformation("User cancelled the file selection");
            return;
        }

        await this.LoadFileIfValid(selectedFile.SelectedFilePath);
    }

    private async Task<bool> EnsurePandocAvailability()
    {
        // Ensure that Pandoc is installed and ready:
        var pandocState = await this.PandocAvailabilityService.EnsureAvailabilityAsync(
            showSuccessMessage: false,
            showDialog: true);

        // Check if Pandoc is available after the check / installation:
        if (!pandocState.IsAvailable)
        {
            this.Logger.LogWarning("The user cancelled the Pandoc installation or Pandoc is not available. Aborting file selection.");
            return false;
        }

        return true;
    }

    private async Task LoadFirstValidFile(List<string> paths)
    {
        if (!await this.EnsurePandocAvailability())
            return;

        foreach (var path in paths)
        {
            if (await this.LoadFileIfValid(path))
                return;
        }
    }

    private async Task<bool> LoadFileIfValid(string filePath)
    {
        if(!File.Exists(filePath))
        {
            this.Logger.LogWarning("Selected file does not exist: '{FilePath}'", filePath);
            return false;
        }

        if (!await FileExtensionValidation.IsExtensionValidWithNotifyAsync(FileExtensionValidation.UseCase.DIRECTLY_LOADING_CONTENT, filePath))
        {
            this.Logger.LogWarning("User attempted to load unsupported file: {FilePath}", filePath);
            return false;
        }

        try
        {
            var fileContent = await UserFile.LoadFileData(filePath, this.RustService, this.DialogService);
            await this.FileContentChanged.InvokeAsync(fileContent);
            this.Logger.LogInformation("Successfully loaded file content: {FilePath}", filePath);
            return true;
        }
        catch (PromptInjectionBlockedException)
        {
            this.Logger.LogWarning("Blocked suspected prompt injection while loading file content: {FilePath}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to load file content: {FilePath}", filePath);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Error, T("Failed to load file content")));
            return false;
        }
    }

    private bool CanCatchDroppedFile() => this.numDropAreasAboveThis is 0 && (this.isComponentHovered || this.CatchAllDocuments);

    private void SetDragClass() => this.dragClass = $"{DEFAULT_DRAG_CLASS} mud-border-primary border-2";

    private void ClearDragClass() => this.dragClass = DEFAULT_DRAG_CLASS;

    private void OnMouseEnter(EventArgs _)
    {
        if(this.Disabled || this.numDropAreasAboveThis > 0)
            return;

        this.Logger.LogDebug("Read file content component is hovered.");
        this.isComponentHovered = true;
        this.SetDragClass();
        this.StateHasChanged();
    }

    private void OnMouseLeave(EventArgs _)
    {
        if(this.Disabled)
            return;

        this.Logger.LogDebug("Read file content component is no longer hovered.");
        this.isComponentHovered = false;
        this.ClearDragClass();
        this.StateHasChanged();
    }
}