using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

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

    [Inject]
    public RustService RustService { get; set; } = null!;
    
    [Inject]
    protected ILogger<SelectDirectory> Logger { get; init; } = null!;
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
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
        var response = await this.RustService.SelectDirectory(this.DirectoryDialogTitle, string.IsNullOrWhiteSpace(this.Directory) ? null : this.Directory);
        this.Logger.LogInformation($"The user selected the directory '{response.SelectedDirectory}'.");

        if (!response.UserCancelled)
            this.InternalDirectoryChanged(response.SelectedDirectory);
    }
}