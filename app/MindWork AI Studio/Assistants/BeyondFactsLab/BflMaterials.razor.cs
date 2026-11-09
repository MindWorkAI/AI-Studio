using System.Globalization;

using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The area "Material" beside the steps of a preparation, where the person adds documents and recordings which the
/// chats of every step read, and removes them again.
/// </summary>
/// <remarks>
/// Reading a document or transcribing a recording runs in BflMaterialService, so it goes on while the person looks at
/// another step or page; this component shows the material and renders whenever the service says it changed.
/// </remarks>
public partial class BflMaterials : MSGComponentBase
{
    /// <summary>
    /// The kinds of files the lab reads: documents, and audio or video recordings for their transcript.
    /// </summary>
    private static readonly FileTypeFilter[] ALLOWED_FILE_TYPES = [FileTypes.DOCUMENT, FileTypes.AUDIO, FileTypes.VIDEO];

    /// <summary>
    /// Gets or sets the ID of the preparation whose material to show.
    /// </summary>
    [Parameter, EditorRequired]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the step shown, whose prompt decides how much of the material fits.
    /// </summary>
    [Parameter, EditorRequired]
    public BflStep Step { get; set; }

    /// <summary>
    /// Gets or sets the provider which answers in the chats of the preparation.
    /// </summary>
    [Parameter, EditorRequired]
    public AIStudio.Settings.Provider Provider { get; set; } = Settings.Provider.NONE;

    /// <summary>
    /// Gets or sets why the provider cannot answer, or null when it can.
    /// </summary>
    [Parameter]
    public string? ProviderIssue { get; set; }

    [Inject]
    private BflMaterialService MaterialService { get; init; } = null!;

    [Inject]
    private BflConversationService ConversationService { get; init; } = null!;

    [Inject]
    private BeyondFactsLabStore Store { get; init; } = null!;

    [Inject]
    private PandocAvailabilityService PandocAvailability { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<BflMaterials> Logger { get; init; } = null!;

    /// <summary>
    /// The documents the person just dropped or chose; they leave as soon as the lab begins to read them.
    /// </summary>
    private readonly HashSet<FileAttachment> attachments = [];

    /// <summary>
    /// The material shown.
    /// </summary>
    private IReadOnlyList<BflMaterialEntry> materials = [];

    /// <summary>
    /// How much of each material the prompt of the step shown carries, by the IDs of the materials; empty while no
    /// provider can answer.
    /// </summary>
    private IReadOnlyDictionary<Guid, BflMaterialShare> shares = new Dictionary<Guid, BflMaterialShare>();

    /// <summary>
    /// The preparation whose material is shown, to notice when the parent shows another one.
    /// </summary>
    private Guid shownProjectId = Guid.Empty;

    /// <summary>
    /// The step the shares were decided for, to decide them again for another step.
    /// </summary>
    private BflStep? sharedStep;

    /// <summary>
    /// The provider the shares were decided for, to decide them again for another provider.
    /// </summary>
    private string sharedProviderId = string.Empty;

    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// Gets the file the lab reads for the preparation right now, or null while it reads none.
    /// </summary>
    private BflMaterialInProgress? MaterialInProgress => this.MaterialService.MaterialInProgress(this.ProjectId);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.MaterialService.MaterialsChanged += this.OnMaterialsChangedAsync;
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (this.shownProjectId != this.ProjectId)
        {
            this.shownProjectId = this.ProjectId;
            this.materials = [];
            await this.ReloadAsync();
        }
        else if (this.sharedStep != this.Step || this.sharedProviderId != this.Provider.Id)
            await this.ShareAsync();
    }

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.MaterialService.MaterialsChanged -= this.OnMaterialsChangedAsync;
        base.DisposeResources();
    }

    /// <summary>
    /// Shows the material again when the service changed it.
    /// </summary>
    /// <param name="projectId">The ID of the preparation whose material changed.</param>
    private Task OnMaterialsChangedAsync(Guid projectId)
    {
        if (projectId != this.ProjectId)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            await this.ReloadAsync();
            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Reads the list of the material of the preparation.
    /// </summary>
    private async Task ReloadAsync()
    {
        try
        {
            this.materials = await this.Store.ListMaterialsAsync(this.ProjectId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not list the material of a preparation of the BeyondFacts Lab.");
            this.materials = [];
        }

        await this.ShareAsync();
    }

    /// <summary>
    /// Decides how much of each material the prompt of the step shown carries, for the provider chosen.
    /// </summary>
    private async Task ShareAsync()
    {
        this.sharedStep = this.Step;
        this.sharedProviderId = this.Provider.Id;
        if (this.ProviderIssue is not null || !this.materials.Any(entry => entry.IsAvailable))
        {
            this.shares = new Dictionary<Guid, BflMaterialShare>();
            return;
        }

        try
        {
            var decided = await this.ConversationService.ShareMaterialsAsync(new BflStepThreadLocation(this.ProjectId, this.Step), this.Provider);
            this.shares = decided.ToDictionary(share => share.Material.MaterialId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            // Sending reports the same problem; the area only leaves out how much fits:
            this.Logger.LogDebug(exception, "Could not decide how much of the material of the BeyondFacts Lab fits.");
            this.shares = new Dictionary<Guid, BflMaterialShare>();
        }
    }

    /// <summary>
    /// Gets what the person needs to know about how much of a material the model reads, or null when it reads all.
    /// </summary>
    /// <param name="material">The material.</param>
    private string? ShareNotice(BflMaterial material)
    {
        if (!this.shares.TryGetValue(material.MaterialId, out var share) || share.IsComplete)
            return null;

        return share.Characters > 0
            ? string.Format(T("Only its first {0} % fits into the context window of the selected model."), share.Percent)
            : T("Nothing of it fits into the context window of the selected model; the model learns only its name.");
    }

    /// <summary>
    /// Lets the lab read the documents the person just dropped or chose.
    /// </summary>
    /// <param name="attached">The documents, as the attachment control collected them.</param>
    private Task AttachedAsync(HashSet<FileAttachment> attached)
    {
        var paths = attached.Where(attachment => attachment is { IsForbidden: false, IsImage: false }).Select(attachment => attachment.FilePath).ToList();
        attached.Clear();
        if (paths.Count is 0)
            return Task.CompletedTask;

        // The service goes on reading when the person leaves the page, so nothing here waits for it:
        this.MaterialService.ImportAsync(this.ProjectId, paths, this.PandocAvailability).Observe("BeyondFacts Lab: reading material");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Lets the lab transcribe the recordings the person just dropped or chose, after asking.
    /// </summary>
    /// <remarks>
    /// The lab asks for itself instead of the attachment control: only a transcription provider with the confidence of
    /// the lab may hear the recordings, and the person learns which one that is before anything leaves the computer.
    /// </remarks>
    /// <param name="paths">The recordings.</param>
    private async Task RecordingsAttachedAsync(IReadOnlyList<string> paths)
    {
        if (this.MaterialService.UsableTranscriptionProvider() is not { } provider)
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, this.MaterialService.NoTranscriptionProviderMessage));
            return;
        }

        var message = string.Format(TA("The lab prepares these recordings on your computer and then uploads their audio to the transcription provider '{0}'. It keeps only their transcript."), Markdown.EscapeInlineText(provider.Name));
        var names = string.Join('\n', paths.Select(path => $"- {Markdown.EscapeInlineText(BeyondFactsLabStore.MaterialName(path))}"));
        var parameters = new DialogParameters<ConfirmDialog>
        {
            {
                dialog => dialog.MarkdownBody,
                $"""
                 {message}

                 {names}
                 """
            },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Transcribe recordings"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        // The service goes on transcribing when the person leaves the page, so nothing here waits for it:
        this.MaterialService.ImportAsync(this.ProjectId, paths, this.PandocAvailability).Observe("BeyondFacts Lab: transcribing material");
    }

    /// <summary>
    /// Gets the icon of a material, which tells its kind.
    /// </summary>
    /// <param name="entry">The material.</param>
    private static string Icon(BflMaterialEntry entry) => entry.Material?.Kind switch
    {
        null => Icons.Material.Filled.ErrorOutline,
        BflMaterialKind.RECORDING => Icons.Material.Filled.Mic,
        _ => Icons.Material.Filled.Description,
    };

    /// <summary>
    /// Removes a material after asking.
    /// </summary>
    /// <param name="entry">The material.</param>
    private async Task DeleteAsync(BflMaterialEntry entry)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            {
                dialog => dialog.Message,
                entry.Material is { } material
                    ? string.Format(TA("Remove '{0}' from the material of this preparation? The file on your computer stays as it is."), material.Name)
                    : TA("AI Studio cannot open this material. Remove it with its files?")
            },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Remove material"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        await this.MaterialService.DeleteAsync(this.ProjectId, entry.MaterialId);
    }

    /// <summary>
    /// Gets the line below the name of a material: its size and when the person added it.
    /// </summary>
    /// <param name="material">The material.</param>
    private string Caption(BflMaterial material) => string.Format(Culture, T("{0}, added on {1}"), material.SourceBytes.FileSize(), material.AddedAtUtc.ToLocalTime().ToString("d", Culture));
}