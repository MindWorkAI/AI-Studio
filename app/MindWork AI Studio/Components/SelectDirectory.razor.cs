using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

// This component has a parameter called Directory, which would shadow the file system's Directory class:
using IODirectory = System.IO.Directory;

namespace AIStudio.Components;

public partial class SelectDirectory : MSGComponentBase
{
    [Parameter]
    public string Directory { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> DirectoryChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string DirectoryDialogTitle { get; set; } = "Select Directory";
    
    [Parameter]
    public Func<string, string?> Validation { get; set; } = _ => null;

    /// <summary>
    /// When true, the folder can also be chosen by dropping it onto this component.
    /// </summary>
    [Parameter]
    public bool EnableDragDrop { get; set; }

    /// <summary>
    /// Makes this component the default target of its area, meaning of its page, assistant, or
    /// dialog: it then also takes the drops which land anywhere in that area without hitting a zone
    /// of their own.
    /// </summary>
    [Parameter]
    public bool CatchAllDocuments { get; set; }

    [Inject]
    public RustService RustService { get; set; } = null!;
    
    [Inject]
    protected ILogger<SelectDirectory> Logger { get; init; } = null!;
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    private bool isDirectoryDialogOpen;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        await base.OnInitializedAsync();
    }

    #endregion

    private void InternalDirectoryChanged(string directory)
    {
        this.Directory = directory;
        this.DirectoryChanged.InvokeAsync(directory);
    }

    private async Task OpenDirectoryDialog()
    {
        if (this.isDirectoryDialogOpen)
            return;

        this.isDirectoryDialogOpen = true;
        try
        {
            var response = await this.RustService.SelectDirectory(this.DirectoryDialogTitle, string.IsNullOrWhiteSpace(this.Directory) ? null : this.Directory);
            this.Logger.LogInformation("The user selected the directory '{SelectedDirectory}'.", response.SelectedDirectory);

            if (!response.UserCancelled)
                this.InternalDirectoryChanged(response.SelectedDirectory);
        }
        finally
        {
            this.isDirectoryDialogOpen = false;
        }
    }

    /// <summary>
    /// Takes the first dropped path which leads to a folder.
    /// </summary>
    /// <remarks>
    /// A dropped file is rejected instead of being taken as its parent folder. Everything a folder
    /// contains is processed, so guessing the parent of a mistakenly dropped file could pull in far
    /// more data than the user meant to hand over.
    /// </remarks>
    /// <param name="paths">The dropped paths.</param>
    private async Task PathsDropped(List<string> paths)
    {
        foreach (var path in paths)
        {
            if (!IODirectory.Exists(path))
                continue;

            this.Logger.LogInformation("The user dropped the directory '{DroppedDirectory}'.", path);
            this.InternalDirectoryChanged(path);
            return;
        }

        this.Logger.LogWarning("None of the {Count} dropped path(s) could be used as a directory.", paths.Count);
        await this.MessageBus.SendWarning(new(Icons.Material.Filled.Warning, this.GetDropWarning(paths)));
    }

    private string GetDropWarning(List<string> paths)
    {
        // Naming the actual mistake beats a generic "that did not work". Dropping a file onto a
        // folder picker is the likeliest of them:
        if (paths.Any(File.Exists))
            return T("Please drop a folder, not a file.");

        return T("The dropped folder could not be accessed. Please choose it with the folder chooser instead.");
    }
}