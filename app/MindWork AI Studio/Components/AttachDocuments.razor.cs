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

    [Parameter]
    public HashSet<FileAttachment> DocumentPaths { get; set; } = [];

    [Parameter]
    public EventCallback<HashSet<FileAttachment>> DocumentPathsChanged { get; set; }

    [Parameter]
    public Func<HashSet<FileAttachment>, Task> OnChange { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// Makes this component the default target of its area, meaning of its page, assistant, or
    /// dialog: it then also takes the drops which land anywhere in that area without hitting a zone
    /// of their own.
    /// </summary>
    /// <remarks>
    /// Only one zone per area can hold that role, and if several ask for it, the first one in the
    /// markup gets it.
    /// </remarks>
    [Parameter]
    public bool CatchAllDocuments { get; set; }

    /// <summary>
    /// The area this component lives in, if it lives in one at all.
    /// </summary>
    [CascadingParameter]
    private DropZoneScopeState? Scope { get; set; }

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

    /// <summary>
    /// Gets or sets the optional picker and drop filter applied before standard attachment validation.
    /// </summary>
    [Parameter]
    public FileTypeFilter[]? AllowedFileTypes { get; set; }

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

    private readonly string dropZoneId = $"attach-documents-{Guid.NewGuid():N}";

    private bool isDefaultZone;
    private bool isDraggingOver;
    private bool isFileDialogOpen;
    private MediaImportOwner EffectiveImportOwner => this.OwnerChat is not null
        ? MediaImportOwner.ForChat(this.OwnerChat.ChatId)
        : this.ImportOwner ?? this.fallbackMediaImportOwner;

    private MediaImportTarget EffectiveMediaImportTarget => new(this.EffectiveImportOwner, string.IsNullOrWhiteSpace(this.Name) ? "attachments" : this.Name);

    private bool IsUnavailable => this.Disabled || this.isFileDialogOpen || this.MediaTranscriptionService.IsBusy(this.EffectiveImportOwner);

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MediaTranscriptionService.StateChanged += this.OnMediaImportStateChanged;
        this.ApplyFilters([], [ Event.HIGHLIGHT_DROP_ZONE, Event.PATHS_DROPPED ]);
        this.ClaimDefaultZoneRole();

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
            this.InvokeAsync(async () =>
            {
                await this.SyncCompletedMediaAttachmentsAsync();
                await this.ConsumeStandaloneMediaOutcomeAsync();
                this.StateHasChanged();
            }).Observe($"{nameof(AttachDocuments)}: syncing media attachments");
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

    /// <summary>Reattaches completed owner results after progress updates or navigation.</summary>
    private async Task SyncCompletedMediaAttachmentsAsync()
    {
        var delivery = this.MediaTranscriptionService.GetPendingDelivery(this.EffectiveMediaImportTarget);
        // Owners that persist their own sources have already taken the media over when the batch
        // started, so re-adding the delivered transcripts here would duplicate them.
        var completed = this.EffectiveImportOwner.Kind.PersistsOwnSources()
            ? Array.Empty<FileAttachment>()
            : delivery?.Attachments ?? [];
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

        // Hand the role of the default target back to the area:
        if (this.isDefaultZone)
            this.Scope?.ReleaseDefaultZone(this);

        base.DisposeResources();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
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
                    this.Logger.LogDebug("The attachment zone '{Name}' is unavailable and swallowed {Count} dropped path(s).", this.Name, dropped.Paths.Count);
                    return;
                }

                await this.AddFileBatchAsync(dropped.Paths);
                await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
                await this.OnChange(this.DocumentPaths);
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
        if (!this.CatchAllDocuments || this.Scope is null)
            return;

        this.isDefaultZone = this.Scope.TryBecomeDefaultZone(this);
        if (!this.isDefaultZone)
            this.Logger.LogDebug("The attachment zone '{Name}' asked to be the default target of its area, which another zone already is. It now takes only the drops aimed at itself.", this.Name);
    }

    /// <summary>
    /// Decides whether the named zone is this one.
    /// </summary>
    /// <remarks>
    /// The area counts as this zone as long as this zone is its default target. That is the whole
    /// mechanism behind dropping anywhere in the chat and still landing on the composer.
    /// </remarks>
    /// <param name="zoneId">The ID the hit test reported, or null when it hit nothing.</param>
    private bool IsThisZone(string? zoneId) => zoneId is not null && (zoneId == this.dropZoneId || (this.isDefaultZone && zoneId == this.Scope?.ScopeId));

    /// <summary>
    /// Highlights the zone, or takes the highlight away.
    /// </summary>
    /// <remarks>
    /// The comparison is not for tidiness: a throttled drag-over event arrives about ten times per
    /// second, and without it every one of them would render every zone on the page anew. In the
    /// small form the highlight also swaps the markup, so this keeps the composer from flickering.
    /// </remarks>
    private void ApplyHighlight(bool shouldBeHighlighted)
    {
        var highlighted = shouldBeHighlighted && !this.IsUnavailable;
        if (highlighted == this.isDraggingOver)
            return;

        this.isDraggingOver = highlighted;
        this.dragClass = highlighted ? $"{DEFAULT_DRAG_CLASS} mud-border-primary border-4" : DEFAULT_DRAG_CLASS;
        this.StateHasChanged();
    }

    private const string DEFAULT_DRAG_CLASS = "relative rounded-lg border-2 border-dashed pa-4 mt-4 mud-width-full mud-height-full";

    private string dragClass = DEFAULT_DRAG_CLASS;

    private async Task AddFilesManually()
    {
        if (this.IsUnavailable)
            return;

        this.isFileDialogOpen = true;
        try
        {
            var selectFiles = await this.RustService.SelectFiles(T("Select files to attach"), this.AllowedFileTypes);
            if (selectFiles.UserCancelled)
                return;

            await this.AddFileBatchAsync(selectFiles.SelectedFilePaths);
            await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
            await this.OnChange(this.DocumentPaths);
        }
        finally
        {
            this.isFileDialogOpen = false;
        }
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
        var pathList = paths.ToList();
        if (this.AllowedFileTypes is { Length: > 0 })
        {
            var rejectedPaths = pathList.Where(path => !FileTypes.IsAllowedPath(path, this.AllowedFileTypes)).ToArray();
            pathList.RemoveAll(path => rejectedPaths.Contains(path, StringComparer.Ordinal));
            if (rejectedPaths.Length > 0)
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.Warning, this.T("Some files do not use an allowed format and were not attached.")));
        }

        var inaccessiblePaths = pathList.Where(path => !File.Exists(path)).ToList();
        if (inaccessiblePaths.Count > 0)
        {
            this.Logger.LogWarning("Could not access {Count} dropped or selected file(s): {Paths}", inaccessiblePaths.Count, string.Join(", ", inaccessiblePaths));
            await this.MessageBus.SendWarning(new(
                Icons.Material.Filled.Warning,
                this.T("Some files could not be accessed. Please select them with the file chooser instead.")));
        }

        var existingPaths = pathList.Except(inaccessiblePaths).ToList();
        var mediaPaths = existingPaths.Where(IsTranscribableMedia).ToList();
        var regularPaths = existingPaths.Except(mediaPaths).ToList();

        //
        // Only the formats we convert with Pandoc depend on a Pandoc installation. Everything
        // else, PDFs in particular, is read by the Rust runtime itself, so those files must stay
        // attachable without Pandoc.
        //
        var canAddPandocFiles = true;
        if (regularPaths.Any(FileTypes.RequiresPandoc))
        {
            var pandocState = await this.PandocAvailabilityService.EnsureAvailabilityAsync(
                showSuccessMessage: false,
                showDialog: true);
            canAddPandocFiles = pandocState.IsAvailable;
        }

        foreach (var path in regularPaths)
        {
            if (!canAddPandocFiles && FileTypes.RequiresPandoc(path))
            {
                this.Logger.LogWarning("The file '{Path}' needs Pandoc and was not attached.", path);
                continue;
            }

            if (!await FileExtensionValidation.IsExtensionValidWithNotifyAsync(FileExtensionValidation.UseCase.ATTACHING_CONTENT, path, this.ValidateMediaFileTypes, this.Provider))
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

        // Owners that persist their own sources show the file right away and keep it next to the
        // stored document, instead of waiting for the transcription to be delivered back.
        if (this.EffectiveImportOwner.Kind.PersistsOwnSources())
        {
            foreach (var mediaPath in mediaPaths)
                this.DocumentPaths.Add(FileAttachment.FromPath(mediaPath));
            
            await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
            await this.OnChange(this.DocumentPaths);
        }

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