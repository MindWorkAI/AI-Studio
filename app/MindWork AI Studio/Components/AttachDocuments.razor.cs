using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Tools.Media;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

using DialogOptions = Dialogs.DialogOptions;

public partial class AttachDocuments : MSGComponentBase
{
    private readonly MediaImportOwner fallbackMediaImportOwner = new(MediaImportOwnerKind.CHAT, $"attachments:{Guid.NewGuid():N}");

    [CascadingParameter]
    private MediaImportOwner? ImportOwner { get; set; }

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AttachDocuments).Namespace, nameof(AttachDocuments));

    [Parameter]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// On which layer to register the drop area. Higher layers have priority over lower layers.
    /// </summary>
    [Parameter]
    public int Layer { get; set; }

    /// <summary>
    /// When true, pause catching dropped files. Default is false.
    /// </summary>
    [Parameter]
    public bool PauseCatchingDrops { get; set; }

    [Parameter]
    public HashSet<FileAttachment> DocumentPaths { get; set; } = [];

    [Parameter]
    public EventCallback<HashSet<FileAttachment>> DocumentPathsChanged { get; set; }

    [Parameter]
    public Func<HashSet<FileAttachment>, Task> OnChange { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// Catch all documents that are hovered over the AI Studio window and not only over the drop zone.
    /// </summary>
    [Parameter]
    public bool CatchAllDocuments { get; set; }

    [Parameter]
    public bool UseSmallForm { get; set; }

    /// <summary>Whether this control renders its own media status.</summary>
    [Parameter]
    public bool ShowMediaStatus { get; set; } = true;

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// When true, validate media file types before attaching. Default is true. That means that
    /// the user cannot attach unsupported media file types when the provider or model does not
    /// support them. Set it to false in order to disable this validation. This is useful for places
    /// where the user might want to prepare a template.
    /// </summary>
    [Parameter]
    public bool ValidateMediaFileTypes { get; set; } = true;

    [Parameter]
    public AIStudio.Settings.Provider? Provider { get; set; }

    /// <summary>Optional persisted chat that can own transcript files immediately.</summary>
    [Parameter]
    public ChatThread? OwnerChat { get; set; }

    /// <summary>Creates and persists a draft owner after media import confirmation.</summary>
    [Parameter]
    public Func<string, Task<ChatThread?>> EnsureOwnerChatAsync { get; set; } = _ => Task.FromResult<ChatThread?>(null);

    [Inject]
    private ILogger<AttachDocuments> Logger { get; set; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private PandocAvailabilityService PandocAvailabilityService { get; init; } = null!;

    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    private const Placement TOOLBAR_TOOLTIP_PLACEMENT = Placement.Top;
    private static readonly string DROP_FILES_HERE_TEXT = TB("Drop files here to attach them.");

    private uint numDropAreasAboveThis;
    private bool isComponentHovered;
    private bool isDraggingOver;
    private MediaImportOwner EffectiveImportOwner => this.OwnerChat is not null
        ? MediaImportOwner.ForChat(this.OwnerChat.ChatId)
        : this.ImportOwner ?? this.fallbackMediaImportOwner;

    private MediaImportTarget EffectiveMediaImportTarget => new(this.EffectiveImportOwner, string.IsNullOrWhiteSpace(this.Name) ? "attachments" : this.Name);

    private bool IsUnavailable => this.Disabled || this.MediaTranscriptionService.IsBusy(this.EffectiveImportOwner);

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MediaTranscriptionService.StateChanged += this.OnMediaImportStateChanged;
        this.ApplyFilters([], [ Event.TAURI_EVENT_RECEIVED, Event.REGISTER_FILE_DROP_AREA, Event.UNREGISTER_FILE_DROP_AREA ]);

        // Register this drop area:
        await this.MessageBus.SendMessage(this, Event.REGISTER_FILE_DROP_AREA, this.Layer);
        await base.OnInitializedAsync();
    }

    /// <summary>Rehydrates results after the component is assigned another chat or target.</summary>
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        await this.SyncCompletedMediaAttachmentsAsync();
    }

    /// <summary>Refreshes disabled controls when the shared import lane changes.</summary>
    private void OnMediaImportStateChanged(MediaImportOwner owner)
    {
        if (owner == this.EffectiveImportOwner)
            _ = this.InvokeAsync(async () =>
            {
                await this.SyncCompletedMediaAttachmentsAsync();
                await this.ConsumeStandaloneMediaOutcomeAsync();
                this.StateHasChanged();
            });
    }

    /// <summary>Consumes outcomes for dialog-local controls that have no chat or assistant owner surface.</summary>
    private async Task ConsumeStandaloneMediaOutcomeAsync()
    {
        if (this.ImportOwner is not null || this.OwnerChat is not null)
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

        if (outcome.Status is MediaImportStatus.CANCELLED)
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, this.T("The media transcription was canceled.")));
        }
    }

    /// <summary>Reattaches completed owner results after progress updates or navigation.</summary>
    private async Task SyncCompletedMediaAttachmentsAsync()
    {
        var delivery = this.MediaTranscriptionService.GetPendingDelivery(this.EffectiveMediaImportTarget);
        var completed = delivery?.Attachments ?? [];
        var pending = this.OwnerChat?.PendingMediaTranscripts ?? [];
        var changed = false;
        var ownerPendingChanged = false;
        
        foreach (var attachment in completed.Concat(pending))
            changed |= this.DocumentPaths.Add(attachment);

        if (this.OwnerChat is not null)
        {
            foreach (var attachment in completed.OfType<ManagedTranscriptAttachment>())
            {
                if (this.OwnerChat.PendingMediaTranscripts.All(existing => existing.FilePath != attachment.FilePath))
                {
                    this.OwnerChat.PendingMediaTranscripts.Add(attachment);
                    ownerPendingChanged = true;
                }
            }
        }

        if (changed || ownerPendingChanged)
        {
            await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
            await this.OnChange(this.DocumentPaths);
        }

        if (delivery is not null)
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
        if (this.IsUnavailable && triggeredEvent == Event.TAURI_EVENT_RECEIVED)
            return;

        switch (triggeredEvent)
        {
            case Event.REGISTER_FILE_DROP_AREA when sendingComponent != this:
            {
                if(data is int layer && layer > this.Layer)
                {
                    this.numDropAreasAboveThis++;
                    this.PauseCatchingDrops = true;
                }

                break;
            }

            case Event.UNREGISTER_FILE_DROP_AREA when sendingComponent != this:
            {
                if(data is int layer && layer > this.Layer)
                {
                    if(this.numDropAreasAboveThis > 0)
                        this.numDropAreasAboveThis--;

                    if(this.numDropAreasAboveThis is 0)
                        this.PauseCatchingDrops = false;
                }

                break;
            }

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_HOVERED }:
                if(this.PauseCatchingDrops)
                    return;

                if(!this.isComponentHovered && !this.CatchAllDocuments)
                {
                    this.Logger.LogDebug("Attach documents component '{Name}' is not hovered, ignoring file drop hovered event.", this.Name);
                    return;
                }

                this.isDraggingOver = true;
                this.SetDragClass();
                this.StateHasChanged();
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_CANCELED }:
                if(this.PauseCatchingDrops)
                    return;

                this.isDraggingOver = false;
                this.StateHasChanged();
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.WINDOW_NOT_FOCUSED }:
                if(this.PauseCatchingDrops)
                    return;

                this.isDraggingOver = false;
                this.isComponentHovered = false;
                this.ClearDragClass();
                this.StateHasChanged();
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_DROPPED, Payload: var paths }:
                if(this.PauseCatchingDrops)
                    return;

                if(!this.isComponentHovered && !this.CatchAllDocuments)
                {
                    this.Logger.LogDebug("Attach documents component '{Name}' is not hovered, ignoring file drop dropped event.", this.Name);
                    return;
                }

                await this.AddFileBatchAsync(paths);
                await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
                await this.OnChange(this.DocumentPaths);
                this.isDraggingOver = false;
                this.ClearDragClass();
                this.StateHasChanged();
                break;
        }
    }

    #endregion

    private const string DEFAULT_DRAG_CLASS = "relative rounded-lg border-2 border-dashed pa-4 mt-4 mud-width-full mud-height-full";

    private string dragClass = DEFAULT_DRAG_CLASS;

    private async Task AddFilesManually()
    {
        if (this.IsUnavailable)
            return;

        var selectFiles = await this.RustService.SelectFiles(T("Select files to attach"));
        if (selectFiles.UserCancelled)
            return;

        await this.AddFileBatchAsync(selectFiles.SelectedFilePaths);
        await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
        await this.OnChange(this.DocumentPaths);
    }

    private async Task OpenAttachmentsDialog()
    {
        if (this.IsUnavailable)
            return;

        var previousAttachments = this.DocumentPaths.ToHashSet();
        this.DocumentPaths = await ReviewAttachmentsDialog.OpenDialogAsync(this.DialogService, this.DocumentPaths);
        foreach (var removedAttachment in previousAttachments.Except(this.DocumentPaths))
            ManagedTranscriptAttachment.TryDeleteOwnedFile(removedAttachment);
        
        this.ReconcileOwnerPendingTranscripts();
    }

    private async Task ClearAllFiles()
    {
        if (this.IsUnavailable)
            return;

        foreach (var attachment in this.DocumentPaths)
            ManagedTranscriptAttachment.TryDeleteOwnedFile(attachment);
        
        this.DocumentPaths.Clear();
        this.ReconcileOwnerPendingTranscripts();
        await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
        await this.OnChange(this.DocumentPaths);
    }

    private void SetDragClass() => this.dragClass = $"{DEFAULT_DRAG_CLASS} mud-border-primary border-4";

    private void ClearDragClass() => this.dragClass = DEFAULT_DRAG_CLASS;

    private void OnMouseEnter(EventArgs _)
    {
        if(this.IsUnavailable || this.PauseCatchingDrops)
            return;

        this.Logger.LogDebug("Attach documents component '{Name}' is hovered.", this.Name);
        this.isComponentHovered = true;
        this.SetDragClass();
        this.StateHasChanged();
    }

    private void OnMouseLeave(EventArgs _)
    {
        if(this.IsUnavailable || this.PauseCatchingDrops)
            return;

        this.Logger.LogDebug("Attach documents component '{Name}' is no longer hovered.", this.Name);
        this.isComponentHovered = false;
        this.ClearDragClass();
        this.StateHasChanged();
    }

    private async Task RemoveDocument(FileAttachment fileAttachment)
    {
        if (this.IsUnavailable)
            return;

        this.DocumentPaths.Remove(fileAttachment);
        ManagedTranscriptAttachment.TryDeleteOwnedFile(fileAttachment);
        this.ReconcileOwnerPendingTranscripts();

        await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
        await this.OnChange(this.DocumentPaths);
    }

    /// <summary>Keeps persisted chat-draft transcript references aligned with the composer.</summary>
    private void ReconcileOwnerPendingTranscripts()
    {
        if (this.OwnerChat is null)
            return;

        var retainedPaths = this.DocumentPaths.Select(attachment => attachment.FilePath).ToHashSet(StringComparer.Ordinal);
        this.OwnerChat.PendingMediaTranscripts.RemoveAll(attachment => !retainedPaths.Contains(attachment.FilePath));
    }

    private async Task AddFileBatchAsync(IEnumerable<string> paths)
    {
        var existingPaths = paths.Where(File.Exists).ToList();
        var mediaPaths = existingPaths.Where(IsTranscribableMedia).ToList();
        var regularPaths = existingPaths.Except(mediaPaths).ToList();

        var canAddRegularFiles = true;
        if (regularPaths.Count > 0)
        {
            var pandocState = await this.PandocAvailabilityService.EnsureAvailabilityAsync(
                showSuccessMessage: false,
                showDialog: true);
            canAddRegularFiles = pandocState.IsAvailable;
        }

        foreach (var path in regularPaths)
        {
            if (!canAddRegularFiles)
                break;

            if (!await FileExtensionValidation.IsExtensionValidWithNotifyAsync(
                    FileExtensionValidation.UseCase.ATTACHING_CONTENT,
                    path,
                    this.ValidateMediaFileTypes,
                    this.Provider))
                continue;
            this.DocumentPaths.Add(FileAttachment.FromPath(path));
        }

        if (mediaPaths.Count is 0)
            return;

        if (string.IsNullOrWhiteSpace(this.SettingsManager.ConfigurationData.App.UseTranscriptionProvider))
        {
            await this.MessageBus.SendWarning(new(
                Icons.Material.Filled.VoiceChat,
                this.T("Media files require a configured transcription provider. Configure one in the transcription settings.")));
            return;
        }

        var names = string.Join('\n', mediaPaths.Select(path => $"- {Markdown.EscapeInlineText(Path.GetFileName(path))}"));
        var message = this.T("The selected audio and video files will be prepared locally. Their audio will then be uploaded to the configured transcription provider.");
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            {
                x => x.MarkdownBody,
                $"""
                 {message}

                 {names}
                 """
            },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(
            this.T("Transcribe media files"),
            dialogParameters,
            DialogOptions.FULLSCREEN);

        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        if (this.OwnerChat is null)
            this.OwnerChat = await this.EnsureOwnerChatAsync(mediaPaths[0]);

        this.MediaTranscriptionService.TryStartAttachmentBatch(mediaPaths, this.EffectiveMediaImportTarget, this.OwnerChat);
    }

    private static bool IsTranscribableMedia(string path) => FileTypes.IsAllowedPath(path, FileTypes.AUDIO) || FileTypes.IsAllowedPath(path, FileTypes.VIDEO);

    /// <summary>
    /// The user might want to check what we actually extract from his file and therefore give the LLM as an input.
    /// </summary>
    /// <param name="fileAttachment">The file to check.</param>
    private async Task InvestigateFile(FileAttachment fileAttachment)
    {
        var dialogParameters = new DialogParameters<DocumentCheckDialog>
        {
            { x => x.Document, fileAttachment },
        };

        await this.DialogService.ShowAsync<DocumentCheckDialog>(T("Document Preview"), dialogParameters, DialogOptions.FULLSCREEN);
    }
}