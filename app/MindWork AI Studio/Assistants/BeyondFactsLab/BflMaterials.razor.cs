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
/// The area "Material" beside the steps of a preparation, where the person adds documents which the chats of every
/// step read, and removes them again.
/// </summary>
/// <remarks>
/// Reading a document runs in BflMaterialService, so it goes on while the person looks at another step or page; this
/// component shows the material and renders whenever the service says it changed.
/// </remarks>
public partial class BflMaterials : MSGComponentBase
{
    /// <summary>
    /// The kinds of files the lab reads: documents, not pictures, audio, or video.
    /// </summary>
    private static readonly FileTypeFilter[] ALLOWED_FILE_TYPES = [FileTypes.DOCUMENT];

    /// <summary>
    /// Gets or sets the ID of the preparation whose material to show.
    /// </summary>
    [Parameter, EditorRequired]
    public Guid ProjectId { get; set; }

    [Inject]
    private BflMaterialService MaterialService { get; init; } = null!;

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
    /// The preparation whose material is shown, to notice when the parent shows another one.
    /// </summary>
    private Guid shownProjectId = Guid.Empty;

    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// Gets the name of the document the lab reads for the preparation right now, or null while it reads none.
    /// </summary>
    private string? DocumentInProgress => this.MaterialService.DocumentInProgress(this.ProjectId);

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
        if (this.shownProjectId == this.ProjectId)
            return;

        this.shownProjectId = this.ProjectId;
        this.materials = [];
        await this.ReloadAsync();
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
                    ? string.Format(TA("Remove '{0}' from the material of this preparation? The document on your computer stays as it is."), material.Name)
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