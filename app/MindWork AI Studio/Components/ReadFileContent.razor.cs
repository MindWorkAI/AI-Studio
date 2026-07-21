using AIStudio.Dialogs;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ReadFileContent : MSGComponentBase
{
    private readonly MediaImportOwner fallbackMediaImportOwner = new(MediaImportOwnerKind.ASSISTANT, $"read-file-content:{Guid.NewGuid():N}");

    [CascadingParameter]
    private MediaImportOwner? ImportOwner { get; set; }

    [Parameter]
    public string MediaImportTargetId { get; set; } = string.Empty;

    [Parameter]
    public string Text { get; set; } = string.Empty;
    
    [Parameter]
    public string FileContent { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> FileContentChanged { get; set; }

    /// <summary>
    /// If true, the component will display the state of the attached document (if any).
    /// </summary>
    [Parameter]
    public bool ShowAttachedDocumentState { get; set; }

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

    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    private const string DEFAULT_DRAG_CLASS = "relative rounded-lg border-2 border-dashed pa-3 mb-3 mud-width-full";

    private string ButtonText => string.IsNullOrWhiteSpace(this.Text) ? T("Use file content as input") : this.Text;
    private string dragClass = DEFAULT_DRAG_CLASS;
    private uint numDropAreasAboveThis;
    private bool isComponentHovered;
    private bool isFileDialogOpen;
    private bool hasLoadedFileContent;
    private string loadedFileName = string.Empty;
    
    private bool IsCurrentTargetBusy => this.MediaTranscriptionService.GetSnapshot(this.EffectiveImportOwner) is { IsBusy: true } snapshot
                                        && snapshot.Target == this.EffectiveMediaImportTarget;
    
    private bool IsUnavailable => this.Disabled || this.isFileDialogOpen || this.MediaTranscriptionService.IsBusy(this.EffectiveImportOwner);

    private MediaImportOwner EffectiveImportOwner => this.ImportOwner ?? this.fallbackMediaImportOwner;
    
    private string EffectiveMediaImportTargetId => string.IsNullOrWhiteSpace(this.MediaImportTargetId)
        ? string.IsNullOrWhiteSpace(this.Text) ? "primary" : this.Text
        : this.MediaImportTargetId;

    private MediaImportTarget EffectiveMediaImportTarget => new(this.EffectiveImportOwner, this.EffectiveMediaImportTargetId);
    
    #region Overrides of MSGComponentBase

    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(this.FileContent))
        {
            this.hasLoadedFileContent = false;
            this.loadedFileName = string.Empty;
        }

        base.OnParametersSet();
    }

    protected override async Task OnInitializedAsync()
    {
        this.MediaTranscriptionService.StateChanged += this.OnMediaImportStateChanged;
        if (this.EnableDragDrop)
        {
            this.ApplyFilters([], [ Event.TAURI_EVENT_RECEIVED, Event.REGISTER_FILE_DROP_AREA, Event.UNREGISTER_FILE_DROP_AREA ]);
            await this.MessageBus.SendMessage(this, Event.REGISTER_FILE_DROP_AREA, this.Layer);
        }

        await base.OnInitializedAsync();
        await this.SyncCompletedMediaTextAsync();
    }

    /// <summary>Refreshes disabled controls when the shared import lane changes.</summary>
    private void OnMediaImportStateChanged(MediaImportOwner owner)
    {
        if (owner == this.EffectiveImportOwner)
            _ = this.InvokeAsync(async () =>
            {
                await this.SyncCompletedMediaTextAsync();
                await this.ConsumeStandaloneMediaOutcomeAsync();
                this.StateHasChanged();
            });
    }

    /// <summary>Consumes outcomes for dialog-local controls that have no assistant owner surface.</summary>
    private async Task ConsumeStandaloneMediaOutcomeAsync()
    {
        if (this.ImportOwner is not null)
            return;

        var outcome = this.MediaTranscriptionService.TryConsumeOutcome(this.EffectiveImportOwner);
        if (outcome is null)
            return;

        if (outcome.Failures.Count > 0)
        {
            var message = string.Join(Environment.NewLine, outcome.Failures.Select(failure => $"{failure.FileName}: {failure.UserMessage}"));
            await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, message));
        }
        else if (outcome.Status is MediaImportStatus.FAILED)
        {
            await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, this.T("The media file could not be transcribed.")));
        }

        if (outcome.Warnings.Count > 0)
        {
            var message = string.Join(Environment.NewLine, outcome.Warnings.Select(warning => $"{warning.FileName}: {warning.UserMessage}"));
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, message));
        }

        if (outcome.Status is MediaImportStatus.CANCELLED)
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, this.T("The media transcription was canceled.")));
        }
    }

    /// <summary>Applies a completed target transcript after progress or navigation.</summary>
    private async Task SyncCompletedMediaTextAsync()
    {
        var delivery = this.MediaTranscriptionService.GetPendingDelivery(this.EffectiveMediaImportTarget);
        if (delivery is null || delivery.Text is not { } text)
            return;

        var fileName = this.MediaTranscriptionService.GetSnapshot(this.EffectiveImportOwner) is { Target: var target } snapshot
                       && target == this.EffectiveMediaImportTarget
            ? snapshot.CurrentFileName
            : string.Empty;
        await this.ApplyFileContentAsync(text, fileName);
        this.MediaTranscriptionService.AcknowledgeDelivery(delivery);
    }

    /// <summary>Unsubscribes from the singleton media service.</summary>
    protected override void DisposeResources()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnMediaImportStateChanged;
        base.DisposeResources();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (!this.EnableDragDrop)
            return;

        if (this.IsUnavailable && triggeredEvent == Event.TAURI_EVENT_RECEIVED)
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
        if (this.IsUnavailable)
            return;

        this.isFileDialogOpen = true;
        try
        {
            var selectedFile = await this.RustService.SelectFile(T("Select file to read its content"));
            if (selectedFile.UserCancelled)
            {
                this.Logger.LogInformation("User cancelled the file selection");
                return;
            }

            await this.LoadFileIfValid(selectedFile.SelectedFilePath);
        }
        finally
        {
            this.isFileDialogOpen = false;
        }
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
        var inaccessiblePaths = paths.Where(path => !File.Exists(path)).ToList();
        if (inaccessiblePaths.Count > 0)
        {
            this.Logger.LogWarning("Could not access {Count} dropped file(s): {Paths}", inaccessiblePaths.Count, string.Join(", ", inaccessiblePaths));
            await this.MessageBus.SendWarning(new(
                Icons.Material.Filled.Warning,
                this.T("Some dropped files could not be accessed. Please select them with the file chooser instead.")));
        }

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

        if (FileTypes.IsAllowedPath(filePath, FileTypes.AUDIO) || FileTypes.IsAllowedPath(filePath, FileTypes.VIDEO))
            return await this.LoadMediaTranscriptAsync(filePath);

        if (!await this.EnsurePandocAvailability())
            return false;

        if (!await FileExtensionValidation.IsExtensionValidWithNotifyAsync(FileExtensionValidation.UseCase.DIRECTLY_LOADING_CONTENT, filePath))
        {
            this.Logger.LogWarning("User attempted to load unsupported file: {FilePath}", filePath);
            return false;
        }

        try
        {
            var fileContent = await UserFile.LoadFileData(filePath, this.RustService, this.DialogService);
            await this.ApplyFileContentAsync(fileContent, filePath);
            this.Logger.LogInformation("Successfully loaded file content: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to load file content: {FilePath}", filePath);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Error, T("Failed to load file content")));
            return false;
        }
    }

    private async Task ApplyFileContentAsync(string fileContent, string filePath)
    {
        await this.FileContentChanged.InvokeAsync(fileContent);
        this.loadedFileName = Path.GetFileName(filePath);
        this.hasLoadedFileContent = true;
    }

    private async Task<bool> LoadMediaTranscriptAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(this.SettingsManager.ConfigurationData.App.UseTranscriptionProvider))
        {
            await this.MessageBus.SendWarning(new(
                Icons.Material.Filled.VoiceChat,
                this.T("Media files require a configured transcription provider. Configure one in the transcription settings.")));
            return false;
        }

        var message = this.T("The selected media file will be prepared locally. Its audio will then be uploaded to the configured transcription provider.");
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            {
                x => x.MarkdownBody,
                $"""
                 {message}

                 - {Markdown.EscapeInlineText(Path.GetFileName(filePath))}
                 """
            },
        };
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(
            this.T("Transcribe media file"),
            dialogParameters,
            Dialogs.DialogOptions.FULLSCREEN);

        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return false;

        return this.MediaTranscriptionService.TryStartTextImport(
            filePath,
            this.EffectiveMediaImportTarget);
    }

    private string FileLoadedTooltip()
    {
        if (!this.hasLoadedFileContent)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(this.loadedFileName))
            return this.T("File content loaded");

        return string.Format(this.T("Attached file '{0}'."), this.loadedFileName);
    }

    private bool CanCatchDroppedFile() => this.numDropAreasAboveThis is 0 && (this.isComponentHovered || this.CatchAllDocuments);

    private void SetDragClass() => this.dragClass = $"{DEFAULT_DRAG_CLASS} mud-border-primary border-2";

    private void ClearDragClass() => this.dragClass = DEFAULT_DRAG_CLASS;

    private void OnMouseEnter(EventArgs _)
    {
        if(this.IsUnavailable || this.numDropAreasAboveThis > 0)
            return;

        this.Logger.LogDebug("Read file content component is hovered.");
        this.isComponentHovered = true;
        this.SetDragClass();
        this.StateHasChanged();
    }

    private void OnMouseLeave(EventArgs _)
    {
        if(this.IsUnavailable)
            return;

        this.Logger.LogDebug("Read file content component is no longer hovered.");
        this.isComponentHovered = false;
        this.ClearDragClass();
        this.StateHasChanged();
    }
}