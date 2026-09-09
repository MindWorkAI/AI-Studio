using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

// This component has a parameter called File, which would shadow the file system's File class:
using IOFile = System.IO.File;

namespace AIStudio.Components;

public partial class SelectFile : MSGComponentBase
{
    [Parameter]
    public string File { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> FileChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string FileDialogTitle { get; set; } = "Select File";
    
    [Parameter]
    public FileTypeFilter[]? Filter { get; set; }
    
    [Parameter]
    public Func<string, string?> Validation { get; set; } = _ => null;

    /// <summary>
    /// When true, the file can also be chosen by dropping it onto this component.
    /// </summary>
    [Parameter]
    public bool EnableDragDrop { get; set; }

    /// <summary>
    /// On which layer to register the drop area. Higher layers have priority over lower layers.
    /// </summary>
    [Parameter]
    public int Layer { get; set; } = DropLayers.ROOT;

    /// <summary>
    /// Catch all documents that are hovered over the AI Studio window and not only over the drop zone.
    /// </summary>
    [Parameter]
    public bool CatchAllDocuments { get; set; }

    [Inject]
    public RustService RustService { get; set; } = null!;
    
    [Inject]
    protected ILogger<SelectFile> Logger { get; init; } = null!;
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    private bool isFileDialogOpen;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        await base.OnInitializedAsync();
    }

    #endregion

    private void InternalFileChanged(string file)
    {
        this.File = file;
        this.FileChanged.InvokeAsync(file);
    }

    private async Task OpenFileDialog()
    {
        if (this.isFileDialogOpen)
            return;

        this.isFileDialogOpen = true;
        try
        {
            var response = await this.RustService.SelectFile(this.FileDialogTitle, this.Filter, string.IsNullOrWhiteSpace(this.File) ? null : this.File);
            this.Logger.LogInformation("The user selected the file '{SelectedFilePath}'.", response.SelectedFilePath);

            if (!response.UserCancelled)
                this.InternalFileChanged(response.SelectedFilePath);
        }
        finally
        {
            this.isFileDialogOpen = false;
        }
    }

    /// <summary>
    /// Takes the first dropped path which leads to a usable file.
    /// </summary>
    /// <remarks>
    /// This component carries exactly one file, so a multi-selection cannot be honored as a whole.
    /// A dropped folder is rejected instead of being read as "the first file inside it": the user
    /// was asked for a file, and picking one for them would be a surprise.
    /// </remarks>
    /// <param name="paths">The dropped paths.</param>
    private async Task PathsDropped(List<string> paths)
    {
        foreach (var path in paths)
        {
            if (!IOFile.Exists(path))
                continue;

            if (this.Filter is { Length: > 0 } && !FileTypes.IsAllowedPath(path, this.Filter))
                continue;

            this.Logger.LogInformation("The user dropped the file '{DroppedFilePath}'.", path);
            this.InternalFileChanged(path);
            return;
        }

        this.Logger.LogWarning("None of the {Count} dropped path(s) could be used as a file.", paths.Count);
        await this.MessageBus.SendWarning(new(Icons.Material.Filled.Warning, this.GetDropWarning(paths)));
    }

    private string GetDropWarning(List<string> paths)
    {
        // Naming the actual mistake beats a generic "that did not work". Dropping a folder onto a
        // file picker is the likeliest of them:
        if (paths.Any(Directory.Exists))
            return T("Please drop a file, not a folder.");

        // The file exists, so the file type filter is what turned it down:
        if (paths.Any(IOFile.Exists))
            return T("Please drop a file with a supported file type.");

        return T("The dropped file could not be accessed. Please choose it with the file chooser instead.");
    }
}