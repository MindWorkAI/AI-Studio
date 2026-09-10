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
        this.ApplyFilters([], []);

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
        base.DisposeResources();
    }

    #endregion

    /// <summary>
    /// Attaches what the user dropped on the zone of this component.
    /// </summary>
    /// <param name="paths">The dropped paths, in the order the runtime delivered them.</param>
    private async Task PathsDropped(List<string> paths)
    {
        await this.AddFileBatchAsync(paths);
        await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
        await this.OnChange(this.DocumentPaths);
    }

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