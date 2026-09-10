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
    /// Reports the path after a file was loaded successfully.
    /// </summary>
    [Parameter]
    public EventCallback<string> FilePathLoaded { get; set; }

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
    /// Makes this component the default target of its area, meaning of its page, assistant, or
    /// dialog: it then also takes the drops which land anywhere in that area without hitting a zone
    /// of their own.
    /// </summary>
    /// <remarks>
    /// Only one zone per area can hold that role, and if several ask for it, the first one in the
    /// markup gets it. The flag has no effect without drag and drop being enabled.
    /// </remarks>
    [Parameter]
    public bool CatchAllDocuments { get; set; }

    /// <summary>
    /// The area this component lives in, if it lives in one at all.
    /// </summary>
    [CascadingParameter]
    private DropZoneScopeState? Scope { get; set; }

    /// <summary>
    /// Optionally restricts the file types offered by the native file picker
    /// and accepted by this component.
    /// </summary>
    [Parameter]
    public FileTypeFilter[]? Filter { get; set; }
    
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

    private readonly string dropZoneId = $"read-file-content-{Guid.NewGuid():N}";

    private string ButtonText => string.IsNullOrWhiteSpace(this.Text) ? T("Use file content as input") : this.Text;
    private string dragClass = DEFAULT_DRAG_CLASS;
    private bool isDefaultZone;
    private bool isHighlighted;
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
            this.ApplyFilters([], [ Event.HIGHLIGHT_DROP_ZONE, Event.PATHS_DROPPED ]);
            this.ClaimDefaultZoneRole();
        }

        await base.OnInitializedAsync();
        await this.SyncCompletedMediaTextAsync();
    }

    /// <summary>Refreshes disabled controls when the shared import lane changes.</summary>
    private void OnMediaImportStateChanged(MediaImportOwner owner)
    {
        if (owner == this.EffectiveImportOwner)
            this.InvokeAsync(async () =>
            {
                await this.SyncCompletedMediaTextAsync();
                await this.ConsumeStandaloneMediaOutcomeAsync();
                this.StateHasChanged();
            }).Observe($"{nameof(ReadFileContent)}: syncing transcribed text");
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

    /// <summary>Unsubscribes from the singleton media service and releases the drop area.</summary>
    protected override void DisposeResources()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnMediaImportStateChanged;

        // Hand the role of the default target back to the area:
        if (this.isDefaultZone)
            this.Scope?.ReleaseDefaultZone(this);

        base.DisposeResources();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (!this.EnableDragDrop)
            return;

        switch (triggeredEvent)
        {
            case Event.HIGHLIGHT_DROP_ZONE when data is DropZoneHighlight highlight:
                this.ApplyHighlight(this.IsThisZone(highlight.ZoneId));
                break;

            case Event.PATHS_DROPPED when data is DroppedPaths dropped:
                // Whoever the drop was meant for, the drag is over and no zone stays highlighted:
                this.ApplyHighlight(false);

                if (!this.IsThisZone(dropped.ZoneId))
                    return;

                if (this.IsUnavailable)
                {
                    this.Logger.LogDebug("The file zone '{ZoneId}' is unavailable and swallowed {Count} dropped path(s).", this.dropZoneId, dropped.Paths.Count);
                    return;
                }

                await this.LoadFirstValidFile(dropped.Paths);
                this.StateHasChanged();
                break;
        }
    }

    #endregion

    /// <summary>
    /// Asks the area for the role of its default target, if this component wants it.
    /// </summary>
    private void ClaimDefaultZoneRole()
    {
        if (!this.CatchAllDocuments)
            return;

        if (this.Scope is null)
        {
            //
            // There is nothing to claim: the surrounding page, assistant, or dialog is not a drop
            // area at all. The flag would then do nothing, and silently -- which is how a zone ends
            // up promising a behaviour it cannot deliver. So say it out loud: either the area needs
            // a DropZoneScope, or the flag does not belong here.
            //
            this.Logger.LogWarning("The file zone '{ZoneId}' wants to be the default target of its area, but it does not live in a drop zone scope. Dropping next to this zone will do nothing.", this.dropZoneId);
            return;
        }

        this.isDefaultZone = this.Scope.TryBecomeDefaultZone(this);
        if (!this.isDefaultZone)
            this.Logger.LogDebug("The file zone '{ZoneId}' asked to be the default target of its area, which another zone already is. It now takes only the drops aimed at itself.", this.dropZoneId);
    }

    /// <summary>
    /// Decides whether the named zone is this one.
    /// </summary>
    /// <remarks>
    /// The area counts as this zone as long as this zone is its default target. That is the whole
    /// mechanism behind dropping anywhere in an assistant and still landing here.
    /// </remarks>
    /// <param name="zoneId">The ID the hit test reported, or null when it hit nothing.</param>
    private bool IsThisZone(string? zoneId) => zoneId is not null && (zoneId == this.dropZoneId || (this.isDefaultZone && zoneId == this.Scope?.ScopeId));

    /// <summary>
    /// Highlights the zone, or takes the highlight away.
    /// </summary>
    /// <remarks>
    /// The comparison is not for tidiness: a throttled drag-over event arrives about ten times per
    /// second, and without it every one of them would render every zone on the page anew.
    /// </remarks>
    private void ApplyHighlight(bool shouldBeHighlighted)
    {
        var highlighted = shouldBeHighlighted && !this.IsUnavailable;
        if (highlighted == this.isHighlighted)
            return;

        this.isHighlighted = highlighted;
        this.dragClass = highlighted ? $"{DEFAULT_DRAG_CLASS} mud-border-primary border-2" : DEFAULT_DRAG_CLASS;
        this.StateHasChanged();
    }
    
    private async Task SelectFile()
    {
        if (this.IsUnavailable)
            return;

        this.isFileDialogOpen = true;
        try
        {
            var selectedFile = await this.RustService.SelectFile(T("Select file to read its content"), this.Filter);
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

        if (this.Filter is { Length: > 0 } && !FileTypes.IsAllowedPath(filePath, this.Filter))
        {
            this.Logger.LogWarning("Selected file does not match the configured file type filter: '{FilePath}'", filePath);
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Warning, this.T("Please select a file with a supported file type.")));
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
            var extraction = await UserFile.LoadFileData(filePath, this.RustService, this.PandocAvailabilityService);

            // The failure was already reported by UserFile.LoadFileData, so we only stop here:
            if (!extraction.HasUsableContent)
                return false;

            await this.ApplyFileContentAsync(extraction.Content, filePath);
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
        await this.FilePathLoaded.InvokeAsync(filePath);
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

}
